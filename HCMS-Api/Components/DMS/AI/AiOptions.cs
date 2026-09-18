namespace HCMS_Api.Components.DMS.AI;

/// <summary>
/// Settings for the DMS AI features, bound from the "Ai:Gateway" configuration section.
///
/// The model runs on-premises: nothing here points at a third-party API, and no document text
/// leaves the network. That is the whole reason the gateway exists, and it is the property to
/// preserve if these values are ever changed.
/// </summary>
public sealed class AiOptions
{
    /// <summary>OpenAI-compatible base, e.g. https://10.10.0.110:8443/v1</summary>
    public string BaseUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "";

    /// <summary>
    /// SHA-256 of the gateway's certificate, uppercase hex, no separators.
    ///
    /// The gateway serves a self-signed certificate, so ordinary chain validation cannot be used.
    /// Pinning the exact certificate is what keeps that from degrading into "trust anything":
    /// leave this empty and the provider refuses to run rather than silently accepting any
    /// certificate offered on that address.
    /// </summary>
    public string CertificateSha256 { get; set; } = "";

    /// <summary>
    /// Folder holding the editable behaviour rules (assistant.md, proofread.md). Relative paths
    /// resolve against the application folder. Point this outside the deployment if the rules
    /// should survive a redeploy untouched.
    /// </summary>
    public string PolicyDirectory { get; set; } = "AiPolicies";

    /// <summary>Master switch. Off means the endpoints answer "not enabled", not 500.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// How long to wait for a reply.
    ///
    /// Generous on purpose. The gateway generates at roughly 27 tokens/second on this hardware, so
    /// proofreading a full editor's worth of content (10,000 characters) measured at about 80
    /// seconds on an otherwise idle model -- and several users sharing the GPU makes that longer.
    /// A timeout shorter than the work fails requests that were about to succeed.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// How hard the model should think before answering, for engines that expose the OpenAI
    /// reasoning_effort field.
    ///
    /// "none" by default, and that is not a performance tweak -- it is what makes these features
    /// work at all on the configured model. Reasoning tokens are drawn from the SAME budget as
    /// max_tokens, so a thinking model spends the allowance deliberating and returns an empty or
    /// truncated answer. Measured against the gateway on a short SOP fragment: 1,022 reasoning
    /// tokens and 1,111 total with reasoning on, versus 0 and 75 with it off -- for the same
    /// correction. Proofreading is a mechanical task; there is nothing here to deliberate about.
    ///
    /// Engines that do not know the field ignore it, so leaving it set is safe.
    /// </summary>
    public string ReasoningEffort { get; set; } = "none";

    /// <summary>
    /// Upper bound on generated tokens per request.
    ///
    /// Sized against the model context (8,192 tokens on the current deployment), which has to hold
    /// the prompt AND the reply. A proofread returns the whole fragment back, so input and output
    /// are roughly the same size -- see MaxProofreadCharacters for the other half of that budget.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 4000;

    /// <summary>
    /// Hard cap on the characters of document context sent to the model. Documents are dropped
    /// from the end of the list until the payload fits, and the answer then says so -- a truncated
    /// context must never be presented as a complete one.
    ///
    /// Bounded by the model context, not by taste: ~12,000 characters is about 3,500 tokens, which
    /// leaves room for the question, the instructions and a full answer inside 8,192.
    /// </summary>
    public int MaxContextCharacters { get; set; } = 12_000;

    /// <summary>
    /// How many documents may be described in one context.
    ///
    /// Measured, not chosen: 25 documents render to about 3,400 prompt tokens against this model,
    /// leaving room for the question and a full answer inside its 8,192-token context. Sixty would
    /// have overrun it on a question that matched nothing and fell back to recent documents.
    /// </summary>
    public int MaxContextDocuments { get; set; } = 25;

    /// <summary>Longest question accepted from a user.</summary>
    public int MaxQuestionCharacters { get; set; } = 2_000;

    /// <summary>
    /// Longest content accepted for a spelling pass.
    ///
    /// A proofread sends the fragment and gets the whole fragment back, so it costs roughly twice
    /// its own size in context. At ~3.4 characters per token, 10,000 characters is about 2,900 in
    /// and 2,900 out, which fits 8,192 with the instructions. Anything longer is refused with a
    /// clear message rather than silently coming back truncated.
    /// </summary>
    public int MaxProofreadCharacters { get; set; } = 10_000;

    /// <summary>Concurrent AI requests allowed across the whole application.</summary>
    public int GlobalConcurrency { get; set; } = 4;

    /// <summary>
    /// Whether the assistant may include a document's actual CONTENT in the model context.
    ///
    /// Default off, deliberately. These are controlled documents -- the SOP template's own footer
    /// carries "This SOP is confidential and private". Answering "show me the content of X" needs
    /// this on; answering "when is X due for review" does not. It is a decision for the document
    /// owner, not a default.
    /// </summary>
    public bool AllowDocumentContent { get; set; }
}
