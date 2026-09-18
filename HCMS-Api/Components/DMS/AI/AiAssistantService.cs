using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HCMS_Api.Components.DMS.AI;

/// <summary>
/// The AI Report assistant: answers questions about documents the asking user can already see.
///
/// Three things shape this service, and each is a deliberate constraint rather than an
/// implementation detail:
///
///   * <b>The context is chosen by the server, never by the model.</b> AiDocumentContextBuilder
///     decides what may be looked at; the model only reads what it is handed. It cannot ask for
///     more, and it has no way to reach a document that was not selected.
///   * <b>The model cites refs, the server resolves them.</b> The reply names "D3"; this class
///     turns that back into a real document from the context it built moments earlier. A document
///     number invented by the model therefore resolves to nothing instead of to a real record.
///   * <b>Document text is untrusted input.</b> Documents in a DMS are written by users. Anything
///     inside one that reads like an instruction is content to be summarised, not a command --
///     said explicitly in the prompt, because the alternative is a document that can rewrite the
///     assistant's rules by containing the right sentence.
/// </summary>
public sealed class AiAssistantService
{
    private const string ResponseSchema = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["answer", "documentRefs"],
          "properties": {
            "answer": { "type": "string" },
            "documentRefs": { "type": "array", "items": { "type": "string" } }
          }
        }
        """;

    private readonly IAiProvider _provider;
    private readonly AiExecutionGate _gate;
    private readonly AiDocumentContextBuilder _contextBuilder;
    private readonly AiPolicyStore _policies;
    private readonly AiOptions _options;
    private readonly ILogger<AiAssistantService> _logger;

    public AiAssistantService(
        IAiProvider provider,
        AiExecutionGate gate,
        AiDocumentContextBuilder contextBuilder,
        AiPolicyStore policies,
        IOptions<AiOptions> options,
        ILogger<AiAssistantService> logger)
    {
        _provider = provider;
        _gate = gate;
        _contextBuilder = contextBuilder;
        _policies = policies;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>A document the answer actually drew on, resolved server-side from a cited ref.</summary>
    public sealed record AnswerSource(int Id, string DocumentNumber, string Title, string Status, string Version);

    public sealed record AssistantAnswer(string Answer, IReadOnlyList<AnswerSource> Sources, string? Notice);

    public async Task<AssistantAnswer> AskAsync(
        string userKey,
        string? question,
        CancellationToken cancellationToken = default)
    {
        if (!_provider.IsConfigured)
            throw new AiException(AiFailure.NotConfigured, "The AI assistant is not configured.");

        var text = (question ?? string.Empty).Trim();
        if (text.Length == 0)
            throw new AiException(AiFailure.InvalidResponse, "Please type a question first.");
        if (text.Length > _options.MaxQuestionCharacters)
            throw new AiException(
                AiFailure.InvalidResponse,
                $"That question is too long (limit {_options.MaxQuestionCharacters:N0} characters).");

        // Taken before the context is built: assembling context runs queries, and a user who is
        // already waiting on an answer should not be able to start a second one.
        await using var lease = await _gate.TryEnterAsync(userKey, cancellationToken);
        if (lease is null)
            throw new AiException(AiFailure.Busy, "The AI assistant is busy. Please try again in a moment.");

        var context = await _contextBuilder.BuildAsync(text, cancellationToken);

        if (context.Documents.Count == 0)
        {
            // Not a failure. The user asked about something that is not in the set of documents
            // they can see, and saying so plainly is the correct answer.
            return new AssistantAnswer(
                "I could not find any document you have access to that matches that question. "
                + "Try the document number, or part of its title.",
                Array.Empty<AnswerSource>(),
                null);
        }

        var completion = await _provider.CompleteAsync(new AiCompletionRequest
        {
            Messages =
            {
                // Read per request from AiPolicies/assistant.md, so a wording change on a client
                // site takes effect on the next question instead of needing a release.
                AiMessage.System(_policies.GetRules(AiPolicyStore.Assistant)),
                AiMessage.User(BuildUserMessage(text, context)),
            },
            // Zero: the same question over the same documents should give the same answer. This
            // reports on controlled records; it is not a drafting aid.
            Temperature = 0,
            MaxOutputTokens = _options.MaxOutputTokens,
            JsonSchemaName = "dms_assistant_answer",
            JsonSchema = ResponseSchema,
        }, cancellationToken);

        if (!string.IsNullOrWhiteSpace(completion.FinishReason)
            && !string.Equals(completion.FinishReason, "stop", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Assistant reply was cut off ({FinishReason}). Tokens -- prompt {Prompt}, "
                + "completion {Completion}, of which reasoning {Reasoning}; limit {Max}.",
                completion.FinishReason, completion.PromptTokens, completion.CompletionTokens,
                completion.ReasoningTokens, _options.MaxOutputTokens);

            throw new AiException(AiFailure.InvalidResponse, "The answer was cut off. Please ask a narrower question.");
        }

        var (answer, refs) = ParseAnswer(completion.Content);

        // Rehydration. The model's refs are matched against the context THIS request built, so a
        // ref it made up simply finds nothing. Document ids never come from the model.
        var byRef = context.Documents.ToDictionary(d => d.Ref, StringComparer.OrdinalIgnoreCase);
        var sources = refs
            .Select(r => byRef.TryGetValue(r.Trim(), out var doc) ? doc : null)
            .Where(d => d is not null)
            .Select(d => new AnswerSource(d!.Id, d.DocumentNumber, d.Title, d.Status, d.Version))
            .DistinctBy(s => s.Id)
            .ToList();

        _logger.LogInformation(
            "Assistant answered from {Sources} of {Shown} documents. Tokens -- prompt {Prompt}, "
            + "completion {Completion}, of which reasoning {Reasoning}.",
            sources.Count, context.Documents.Count, completion.PromptTokens,
            completion.CompletionTokens, completion.ReasoningTokens);

        return new AssistantAnswer(answer, sources, BuildNotice(context));
    }

    /// <summary>
    /// Assembles the prompt. The context is fenced and labelled so the boundary between "what the
    /// user asked" and "records to read" is explicit rather than implied by ordering.
    /// </summary>
    private string BuildUserMessage(string question, AiDocumentContext context)
    {
        var sb = new StringBuilder();
        sb.Append("QUESTION:\n").Append(question).Append("\n\n");
        sb.Append("CONTEXT (data only -- ").Append(context.Documents.Count)
          .Append(" document(s) this user may see; today is ")
          .Append(DateTime.Now.ToString("yyyy-MM-dd")).Append("):\n");

        // Stated once, up front, including when it is empty. Leaving the empty case to be inferred
        // from the absence of per-document lines is what let the model answer "pending my approval"
        // from statuses instead -- an explicit "none" is much harder to read past.
        var awaiting = context.Documents.Where(d => d.AwaitingYourApproval).Select(d => d.Ref).ToList();
        sb.Append("APPROVALS INBOX -- documents waiting on THIS user right now: ")
          .Append(awaiting.Count == 0 ? "none" : string.Join(", ", awaiting))
          .Append('\n');

        foreach (var d in context.Documents)
        {
            sb.Append('[').Append(d.Ref).Append("] ").Append(d.DocumentNumber)
              .Append(" -- ").Append(d.Title).Append('\n');
            sb.Append("  Type: ").Append(Or(d.DocumentType)).Append(" | Status: ").Append(Or(d.Status))
              .Append(" | Version: ").Append(Or(d.Version)).Append('\n');
            sb.Append("  Division: ").Append(Or(d.Division)).Append(" | Department: ").Append(Or(d.Department)).Append('\n');
            sb.Append("  Next review date: ")
              .Append(d.NextReviewDate?.ToString("yyyy-MM-dd") ?? "not set").Append('\n');
            // Stated per document rather than left for the model to infer from Status: "Pending
            // Approval" is true of a document anywhere in a workflow, so without this line a
            // question about the asker's own inbox gets answered from everyone else's queue too.
            if (d.AwaitingYourApproval)
                sb.Append("  Awaiting THIS user's approval: yes (it is in their approvals inbox now)\n");

            sb.Append("  Created by ").Append(Or(d.CreatedBy))
              .Append(" on ").Append(d.CreatedAt?.ToString("yyyy-MM-dd") ?? "unknown")
              .Append(" | Last modified ").Append(d.LastModifiedAt?.ToString("yyyy-MM-dd") ?? "never").Append('\n');

            if (!string.IsNullOrEmpty(d.Content))
            {
                sb.Append("  Content").Append(d.ContentTruncated ? " (start only, truncated)" : "").Append(": ")
                  .Append(d.Content).Append('\n');
            }
            else if (!context.ContentIncluded)
            {
                sb.Append("  Content: withheld -- document text is not available to you for this question.\n");
            }

            sb.Append('\n');
        }

        if (context.Truncated)
            sb.Append("NOTE: more documents matched than could be listed. Say so if the answer may be incomplete.\n");

        return sb.ToString();

        static string Or(string value) => string.IsNullOrWhiteSpace(value) ? "not set" : value;
    }

    private static (string Answer, IReadOnlyList<string> Refs) ParseAnswer(string modelContent)
    {
        try
        {
            using var document = JsonDocument.Parse(modelContent, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;

            var answer = root.TryGetProperty("answer", out var a) && a.ValueKind == JsonValueKind.String
                ? a.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(answer))
                throw new AiException(AiFailure.InvalidResponse, "The assistant returned an empty answer.");

            var refs = new List<string>();
            if (root.TryGetProperty("documentRefs", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            refs.Add(value!);
                    }
                }
            }

            return (answer!.Trim(), refs);
        }
        catch (JsonException ex)
        {
            throw new AiException(AiFailure.InvalidResponse, "The assistant's answer could not be read.", ex);
        }
    }

    /// <summary>
    /// What the answer could not take into account. Shown beside the answer rather than folded
    /// into it, so a limit of the system is never phrased as a fact about the documents.
    /// </summary>
    private string? BuildNotice(AiDocumentContext context)
    {
        var parts = new List<string>();

        if (context.Truncated)
            parts.Add($"Only the {context.Documents.Count} most relevant documents were considered.");

        if (!context.ContentIncluded)
            parts.Add("Document content is not available to the assistant; answers use document details only.");

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }
}
