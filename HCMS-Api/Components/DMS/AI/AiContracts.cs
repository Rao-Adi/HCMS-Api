namespace HCMS_Api.Components.DMS.AI;

/// <summary>One turn in a model conversation.</summary>
public sealed record AiMessage(string Role, string Content)
{
    public static AiMessage System(string content) => new("system", content);
    public static AiMessage User(string content) => new("user", content);
    public static AiMessage Assistant(string content) => new("assistant", content);
}

public sealed class AiCompletionRequest
{
    public List<AiMessage> Messages { get; set; } = new();

    /// <summary>0 for anything that must be reproducible; a little higher only for drafting.</summary>
    public double Temperature { get; set; }

    public int MaxOutputTokens { get; set; }

    /// <summary>
    /// Per-request override for AiOptions.ReasoningEffort. Null means use the configured value.
    /// Only worth setting when one caller genuinely needs the model to deliberate; the budget it
    /// spends doing so comes out of MaxOutputTokens.
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>
    /// A JSON schema the model's reply must match. Used for the assistant, so the reply can be
    /// parsed and validated instead of scraped out of prose.
    /// </summary>
    public string? JsonSchemaName { get; set; }

    public string? JsonSchema { get; set; }
}

/// <summary>
/// A model reply, with the token accounting that explains it.
///
/// The usage numbers are not decoration: on a reasoning model they are the difference between
/// "the model had nothing to say" and "the model spent its whole allowance thinking", which look
/// identical from the content alone.
/// </summary>
public sealed record AiCompletion(
    string Content,
    string? FinishReason,
    int PromptTokens = 0,
    int CompletionTokens = 0,
    int ReasoningTokens = 0);

public enum AiFailure
{
    /// <summary>No BaseUrl / ApiKey / certificate pin, or the feature is switched off.</summary>
    NotConfigured,

    /// <summary>The gateway could not be reached, or answered 5xx. Retryable.</summary>
    Unavailable,

    /// <summary>The gateway rejected the credentials.</summary>
    Unauthorized,

    /// <summary>Too many requests already in flight.</summary>
    Busy,

    /// <summary>A reply arrived but was unusable -- truncated, malformed, or off-schema.</summary>
    InvalidResponse,
}

/// <summary>
/// Raised instead of letting an AI problem surface as a generic 500. The distinction matters to
/// the caller: "the model is down" is a temporary condition a user should be told about plainly,
/// while "not configured" is an administrator's job and should never look like a fault.
/// </summary>
public sealed class AiException : Exception
{
    public AiFailure Failure { get; }

    public AiException(AiFailure failure, string message, Exception? inner = null)
        : base(message, inner) => Failure = failure;
}

/// <summary>The model boundary. Everything AI-related in DMS goes through this one interface.</summary>
public interface IAiProvider
{
    bool IsConfigured { get; }

    Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default);
}
