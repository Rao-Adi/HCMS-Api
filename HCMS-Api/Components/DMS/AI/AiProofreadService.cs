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

        // An embedded image is hundreds of thousands of characters of base64 that no spelling check needs to see: it is set
        // aside here and put back afterwards, so a document with pictures is not "too long" because of them.
        var masked = MaskImages(content, out var images);

        // MaxProofreadTotalCharacters <= 0 means no limit on the whole content.
        if (_options.MaxProofreadTotalCharacters > 0 && masked.Length > _options.MaxProofreadTotalCharacters)
            throw new AiException(
                AiFailure.InvalidResponse,
                $"The content is too long to proofread (limit {_options.MaxProofreadTotalCharacters:N0} characters, not counting images). Check it in parts.");

        // Content longer than one call can carry is cut at block boundaries (paragraphs, list items, table rows) and each piece is
        // proofread on its own; the pieces are joined again in their original order, around the tags that were cut through.
        var segments = PlanSegments(masked, Math.Min(_options.ProofreadChunkCharacters, _options.MaxProofreadCharacters));
        foreach (var piece in segments.Where(s => s.IsPiece))
            if (piece.Text.Length > _options.MaxProofreadCharacters)
                throw new AiException(
                    AiFailure.InvalidResponse,
                    $"One block of the content is too long to proofread (limit {_options.MaxProofreadCharacters:N0} characters in a single paragraph, list item or table cell). Split it into smaller paragraphs.");

        await using var lease = await _gate.TryEnterAsync(userKey, cancellationToken);
        if (lease is null)
            throw new AiException(AiFailure.Busy, "The AI assistant is busy. Please try again in a moment.");

        // A piece without any words (only tags, spaces) has nothing to correct and is not sent.
        var toSend = segments.Where(s => s.IsPiece && HasWords(s.Text)).ToList();
        using var throttle = new SemaphoreSlim(Math.Clamp(_options.GlobalConcurrency, 1, 4));
        await Task.WhenAll(toSend.Select(async piece =>
        {
            await throttle.WaitAsync(cancellationToken);
            try { piece.Corrected = await ProofreadPieceAsync(piece.Text, cancellationToken); }
            finally { throttle.Release(); }
        }));

        var correctedMasked = string.Concat(segments.Select(s => s.Corrected ?? s.Text));
        var corrected = RestoreImages(correctedMasked, images);
        var changed = !string.Equals(corrected, content, StringComparison.Ordinal);

        _logger.LogInformation(
            "Proofread {InputChars} characters ({Pieces} piece(s), {Images} image(s) set aside); changed = {Changed}.",
            masked.Length, toSend.Count, images.Count, changed);

        return new ProofreadResult(
            corrected,
            changed,
            "AI-generated correction. Review before applying.");
    }

    // One piece through the model, with the same checks as the whole content always had: a reply that was cut off, that holds
    // anything executable, or that changed the tag structure is refused.
    private async Task<string> ProofreadPieceAsync(string content, CancellationToken cancellationToken)
    {
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

        _logger.LogInformation(
            "Proofread a piece of {InputChars} characters; changed = {Changed}. "
            + "Tokens -- prompt {Prompt}, completion {Completion}, of which reasoning {Reasoning}.",
            content.Length, !string.Equals(corrected, content, StringComparison.Ordinal), completion.PromptTokens,
            completion.CompletionTokens, completion.ReasoningTokens);

        return corrected;
    }

    // ── Images, and cutting long content into pieces ────────────────────────────────────────────────────────────────

    // <img src="data:image/png;base64,....">: the (very long) data is replaced by a short token and put back afterwards.
    private static readonly Regex DataImageSrc = new(
        @"(<img\b[^>]*?\bsrc\s*=\s*)(""|')(data:[^""']*)\2",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AnyTag = new(
        @"<!--.*?-->|<\s*(/?)\s*([a-zA-Z][a-zA-Z0-9]*)\b[^>]*?(/?)>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr",
    };

    private static string ImageToken(int index) => "dms-masked-image-" + index + "-";

    private static string MaskImages(string html, out List<string> images)
    {
        var found = new List<string>();
        var masked = DataImageSrc.Replace(html, m =>
        {
            found.Add(m.Groups[3].Value);
            return m.Groups[1].Value + m.Groups[2].Value + ImageToken(found.Count - 1) + m.Groups[2].Value;
        });
        images = found;
        return masked;
    }

    private static string RestoreImages(string html, List<string> images)
    {
        for (var i = 0; i < images.Count; i++)
        {
            var token = ImageToken(i);
            var first = html.IndexOf(token, StringComparison.Ordinal);
            // Exactly once: a token the model dropped, repeated or altered means the picture would be lost or doubled.
            if (first < 0 || html.IndexOf(token, first + token.Length, StringComparison.Ordinal) >= 0)
                throw new AiException(
                    AiFailure.InvalidResponse,
                    "The proofread result changed the document's formatting, so it was discarded.");
            html = html.Replace(token, images[i], StringComparison.Ordinal);
        }
        return html;
    }

    private static bool HasWords(string html) =>
        Regex.IsMatch(Regex.Replace(html, "<[^>]*>", " "), @"[\p{L}\p{N}]");

    /// <summary>A stretch of the content: a piece to proofread, or tags that were cut through and are kept as they are.</summary>
    private sealed class Segment
    {
        public Segment(string text, bool isPiece) { Text = text; IsPiece = isPiece; }
        public string Text { get; }
        public bool IsPiece { get; }
        public string? Corrected { get; set; }
    }

    /// <summary>
    /// The content as an ordered list of segments that, put together, are exactly the content. Pieces are at most
    /// <paramref name="budget"/> characters where the structure allows: neighbouring blocks are grouped up to the budget, and a
    /// block that is larger is entered (its opening and closing tag are kept as they are) and cut at ITS blocks in turn.
    /// A block that has nothing inside to cut at (one very long paragraph of plain text) stays whole.
    /// </summary>
    private static List<Segment> PlanSegments(string html, int budget)
    {
        var flat = new List<Segment>();
        Plan(html, budget, 0, flat);

        // Neighbouring pieces (no tag kept between them) become one, as large as the budget allows -- fewer calls.
        var merged = new List<Segment>();
        foreach (var s in flat)
        {
            if (s.IsPiece && merged.Count > 0 && merged[^1].IsPiece && merged[^1].Text.Length + s.Text.Length <= budget)
                merged[^1] = new Segment(merged[^1].Text + s.Text, true);
            else
                merged.Add(s);
        }
        return merged;
    }

    private static void Plan(string fragment, int budget, int level, List<Segment> into)
    {
        if (fragment.Length == 0) return;
        if (fragment.Length <= budget) { into.Add(new Segment(fragment, true)); return; }

        var blocks = TopLevelBlocks(fragment);
        if (blocks.Count > 1)
        {
            foreach (var block in blocks) Plan(block, budget, level, into);
            return;
        }

        // One block that is too large: enter it, if it is a single element with something inside.
        var tags = AnyTag.Matches(fragment).Where(m => !m.Value.StartsWith("<!--", StringComparison.Ordinal)).ToList();
        if (level < 8 && tags.Count >= 2)
        {
            var open = tags[0];
            var close = tags[^1];
            if (open.Index == 0 && open.Groups[1].Value != "/" && open.Groups[3].Value != "/" && !VoidTags.Contains(open.Groups[2].Value)
                && close.Groups[1].Value == "/" && close.Index + close.Length == fragment.Length
                && string.Equals(open.Groups[2].Value, close.Groups[2].Value, StringComparison.OrdinalIgnoreCase))
            {
                var inner = fragment.Substring(open.Length, close.Index - open.Length);
                into.Add(new Segment(open.Value, false));
                Plan(inner, budget, level + 1, into);
                into.Add(new Segment(close.Value, false));
                return;
            }
        }

        // Plain text with no tags at all (one huge paragraph body): cut at a space or line break near the budget.
        if (tags.Count == 0)
        {
            var pos = 0;
            while (pos < fragment.Length)
            {
                var len = Math.Min(budget, fragment.Length - pos);
                if (pos + len < fragment.Length)
                {
                    var cut = fragment.LastIndexOfAny(new[] { ' ', '\n', '\t' }, pos + len - 1, len);
                    if (cut > pos) len = cut - pos + 1;
                }
                into.Add(new Segment(fragment.Substring(pos, len), true));
                pos += len;
            }
            return;
        }

        into.Add(new Segment(fragment, true));
    }

    /// <summary>The top-level blocks of a fragment, in order: each ends where the element that began it is closed.</summary>
    private static List<string> TopLevelBlocks(string html)
    {
        var blocks = new List<string>();
        var start = 0;
        var depth = 0;
        foreach (Match m in AnyTag.Matches(html))
        {
            if (m.Value.StartsWith("<!--", StringComparison.Ordinal)) continue;
            var name = m.Groups[2].Value;
            var closing = m.Groups[1].Value == "/";
            var selfClosing = m.Groups[3].Value == "/" || VoidTags.Contains(name);
            if (closing) depth = Math.Max(0, depth - 1);
            else if (!selfClosing) depth++;

            if (depth == 0)
            {
                var end = m.Index + m.Length;
                blocks.Add(html.Substring(start, end - start));
                start = end;
            }
        }
        if (start < html.Length) blocks.Add(html.Substring(start));
        return blocks;
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
