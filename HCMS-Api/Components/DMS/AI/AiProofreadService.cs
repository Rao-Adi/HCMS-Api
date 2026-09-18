using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace HCMS_Api.Components.DMS.AI;

/// <summary>
/// The rich text editor's proofreading action: corrects spelling and grammar in document content
/// the user has typed, and returns the corrected HTML for them to review.
///
/// Deliberately generate-only. It returns a suggestion; it never writes the document. On a
/// controlled document, silently rewriting what an author typed is not a convenience -- the author
/// has to see what changed and accept it, which is why the result is handed back rather than saved.
///
/// Two constraints shape the prompt:
///
///   * The HTML structure must survive untouched. This content is later converted to OpenXML and
///     dropped into the Word template (see HtmlToOpenXmlConverter), so a model that "tidied" the
///     markup would change how the issued document is laid out.
///   * Meaning must not change. Fixing "recieve" is the job; rewording a procedure step is not.
///     An SOP says what it says.
/// </summary>
public sealed class AiProofreadService
{
    private const string ResponseSchema = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["correctedHtml", "changed"],
          "properties": {
            "correctedHtml": { "type": "string" },
            "changed": { "type": "boolean" }
          }
        }
        """;

    // Anything that could execute or load in the editor. The model is not expected to produce
    // these, which is exactly why a reply containing one is treated as unusable rather than cleaned
    // up and shown.
    private static readonly Regex UnsafeHtml = new(
        @"<\s*/?\s*(script|iframe|object|embed|link|meta|svg|math)\b|\bon[a-z]+\s*=|javascript:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TagName = new(
        @"<\s*/?\s*([a-zA-Z][a-zA-Z0-9]*)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IAiProvider _provider;
    private readonly AiExecutionGate _gate;
    private readonly AiPolicyStore _policies;
    private readonly AiOptions _options;
    private readonly ILogger<AiProofreadService> _logger;

    public AiProofreadService(
        IAiProvider provider,
        AiExecutionGate gate,
        AiPolicyStore policies,
        IOptions<AiOptions> options,
        ILogger<AiProofreadService> logger)
    {
        _provider = provider;
        _gate = gate;
        _policies = policies;
        _options = options.Value;
        _logger = logger;
    }

    public sealed record ProofreadResult(string CorrectedHtml, bool Changed, string Notice);

    public async Task<ProofreadResult> ProofreadAsync(
        string userKey,
        string? html,
        CancellationToken cancellationToken = default)
    {
        if (!_provider.IsConfigured)
            throw new AiException(AiFailure.NotConfigured, "The AI assistant is not configured.");

        var content = (html ?? string.Empty).Trim();
        if (content.Length == 0)
            throw new AiException(AiFailure.InvalidResponse, "There is no content to proofread.");
        if (content.Length > _options.MaxProofreadCharacters)
            throw new AiException(
                AiFailure.InvalidResponse,
                $"The content is too long to proofread (limit {_options.MaxProofreadCharacters:N0} characters).");

        await using var lease = await _gate.TryEnterAsync(userKey, cancellationToken);
        if (lease is null)
            throw new AiException(AiFailure.Busy, "The AI assistant is busy. Please try again in a moment.");

        var completion = await _provider.CompleteAsync(new AiCompletionRequest
        {
            Messages =
            {
                // Read per run from AiPolicies/proofread.md -- see AiPolicyStore.
                AiMessage.System(_policies.GetRules(AiPolicyStore.Proofread)),
                AiMessage.User("Proofread this HTML fragment:\n\n" + content),
            },
            // Zero, so the same content proofreads to the same result. This is an editing aid on a
            // controlled document, not a creative task.
            Temperature = 0,
            MaxOutputTokens = _options.MaxOutputTokens,
            JsonSchemaName = "dms_proofread_result",
            JsonSchema = ResponseSchema,
        }, cancellationToken);

        if (!string.IsNullOrWhiteSpace(completion.FinishReason)
            && !string.Equals(completion.FinishReason, "stop", StringComparison.OrdinalIgnoreCase))
        {
            // A truncated reply would silently drop the tail of the document. Log what the budget
            // actually went on: if ReasoningTokens is most of it, the model is deliberating rather
            // than answering and AiOptions.ReasoningEffort is the setting to look at.
            _logger.LogWarning(
                "Proofread reply was cut off ({FinishReason}) after {InputChars} characters. "
                + "Tokens -- prompt {Prompt}, completion {Completion}, of which reasoning {Reasoning}; "
                + "limit {MaxOutputTokens}.",
                completion.FinishReason, content.Length, completion.PromptTokens,
                completion.CompletionTokens, completion.ReasoningTokens, _options.MaxOutputTokens);

            throw new AiException(AiFailure.InvalidResponse, "The proofread result was incomplete.");
        }

        var corrected = ParseCorrectedHtml(completion.Content, content);
        var changed = !string.Equals(corrected, content, StringComparison.Ordinal);

        _logger.LogInformation(
            "Proofread {InputChars} characters; changed = {Changed}. "
            + "Tokens -- prompt {Prompt}, completion {Completion}, of which reasoning {Reasoning}.",
            content.Length, changed, completion.PromptTokens,
            completion.CompletionTokens, completion.ReasoningTokens);

        return new ProofreadResult(
            corrected,
            changed,
            "AI-generated correction. Review before applying.");
    }

    private string ParseCorrectedHtml(string modelContent, string original)
    {
        string? corrected;
        try
        {
            using var document = JsonDocument.Parse(modelContent, new JsonDocumentOptions { MaxDepth = 8 });
            corrected = document.RootElement.TryGetProperty("correctedHtml", out var element)
                && element.ValueKind == JsonValueKind.String
                    ? element.GetString()
                    : null;
        }
        catch (JsonException ex)
        {
            throw new AiException(AiFailure.InvalidResponse, "The proofread result could not be read.", ex);
        }

        if (string.IsNullOrWhiteSpace(corrected))
            throw new AiException(AiFailure.InvalidResponse, "The proofread result was empty.");

        corrected = corrected.Trim();

        if (UnsafeHtml.IsMatch(corrected))
            throw new AiException(AiFailure.InvalidResponse, "The proofread result contained unsupported markup.");

        // The model was told to leave the markup alone; this checks that it did. A changed tag set
        // means the structure moved, which would change the issued document's layout, so the
        // correction is rejected rather than applied and noticed later.
        if (!TagsMatch(original, corrected))
        {
            _logger.LogWarning("Proofread result altered the HTML structure; rejecting it.");
            throw new AiException(
                AiFailure.InvalidResponse,
                "The proofread result changed the document's formatting, so it was discarded.");
        }

        return corrected;
    }

    /// <summary>Same tags, same order. Compared as a sequence so a dropped or added tag is caught.</summary>
    private static bool TagsMatch(string left, string right)
    {
        var a = TagName.Matches(left).Select(m => m.Groups[1].Value.ToLowerInvariant()).ToList();
        var b = TagName.Matches(right).Select(m => m.Groups[1].Value.ToLowerInvariant()).ToList();
        return a.SequenceEqual(b, StringComparer.Ordinal);
    }
}
