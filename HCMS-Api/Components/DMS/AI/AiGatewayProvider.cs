using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HCMS_Api.Components.DMS.AI;

/// <summary>
/// Talks to the on-premises model gateway over its OpenAI-compatible API.
///
/// Two things here are deliberate and worth not "simplifying" later:
///
///   * Certificate pinning. The gateway serves a self-signed certificate, so the usual chain
///     validation cannot apply. Rather than switching validation off -- which would accept any
///     certificate anyone can present on that address -- the handler compares the server
///     certificate's SHA-256 against the configured pin. No pin configured means the provider
///     reports itself unconfigured and refuses to send, instead of quietly running unprotected.
///
///   * Failures are typed, not swallowed. A model that is down is a normal operating condition
///     for this feature; the caller turns an AiException into a clear message. What must never
///     happen is an AI problem surfacing as a 500 on a document screen.
/// </summary>
public sealed class AiGatewayProvider : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpFactory;
    private readonly AiOptions _options;
    private readonly ILogger<AiGatewayProvider> _logger;

    public AiGatewayProvider(
        IHttpClientFactory httpFactory,
        IOptions<AiOptions> options,
        ILogger<AiGatewayProvider> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.BaseUrl)
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.Model)
        && !string.IsNullOrWhiteSpace(_options.CertificateSha256);

    /// <summary>Name the pinned HttpClient is registered under (see AiServiceRegistration).</summary>
    public const string HttpClientName = "dms-ai-gateway";

    public async Task<AiCompletion> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsConfigured)
            throw new AiException(AiFailure.NotConfigured, "The AI assistant is not configured.");
        if (request.Messages.Count == 0)
            throw new AiException(AiFailure.InvalidResponse, "No prompt was supplied.");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["temperature"] = request.Temperature,
            ["max_tokens"] = request.MaxOutputTokens > 0
                ? Math.Min(request.MaxOutputTokens, _options.MaxOutputTokens)
                : _options.MaxOutputTokens,
            ["stream"] = false,
            ["messages"] = request.Messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
        };

        // Reasoning tokens are spent out of max_tokens, so on a thinking model this is the
        // difference between a usable reply and an empty one. Engines that do not recognise the
        // field ignore it.
        var reasoningEffort = request.ReasoningEffort ?? _options.ReasoningEffort;
        if (!string.IsNullOrWhiteSpace(reasoningEffort))
            payload["reasoning_effort"] = reasoningEffort;

        // Constrains the reply to a schema the caller can parse. Engines that do not support this
        // ignore the field, which is why every caller still validates what comes back.
        if (!string.IsNullOrWhiteSpace(request.JsonSchema) && !string.IsNullOrWhiteSpace(request.JsonSchemaName))
        {
            using var schema = JsonDocument.Parse(request.JsonSchema);
            payload["response_format"] = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = request.JsonSchemaName,
                    schema = schema.RootElement.Clone(),
                    strict = true,
                }
            };
        }

        var client = _httpFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 600));

        using var message = new HttpRequestMessage(
            HttpMethod.Post, _options.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiException(AiFailure.Unavailable, "The AI assistant timed out. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "AI gateway unreachable at {BaseUrl}.", _options.BaseUrl);
            throw new AiException(AiFailure.Unavailable, "The AI assistant is currently unavailable.", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // 502 here means the proxy is up but the model engine behind it is not running --
                // the exact state this gateway was found in while the feature was being built, so
                // it is worth logging plainly rather than as a generic failure.
                _logger.LogWarning(
                    "AI gateway returned {Status} for model {Model}. Body: {Body}",
                    (int)response.StatusCode, _options.Model, Truncate(body, 300));

                throw response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        new AiException(AiFailure.Unauthorized, "The AI assistant rejected this server's credentials."),
                    HttpStatusCode.TooManyRequests =>
                        new AiException(AiFailure.Busy, "The AI assistant is busy. Please try again in a moment."),
                    _ => new AiException(AiFailure.Unavailable, "The AI assistant is currently unavailable."),
                };
            }

            return ParseCompletion(body);
        }
    }

    private static AiCompletion ParseCompletion(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var usage = ReadUsage(document.RootElement);
            var choice = document.RootElement.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0
                    ? choices[0]
                    : throw new AiException(AiFailure.InvalidResponse, "The AI assistant returned no answer.");

            var content = choice.TryGetProperty("message", out var msg)
                && msg.TryGetProperty("content", out var text)
                    ? text.GetString()
                    : null;

            var finishReason = choice.TryGetProperty("finish_reason", out var reason)
                ? reason.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(content))
            {
                // Separating these two is what makes the failure diagnosable. "length" with no
                // content means the reply hit max_tokens before any of it was written -- on a
                // reasoning model, usually because deliberation consumed the whole allowance. That
                // is a budget to raise or reasoning to switch off, not a model that said nothing.
                var ranOut = string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase);
                throw new AiException(
                    AiFailure.InvalidResponse,
                    ranOut
                        ? "The AI assistant ran out of room before it could answer. Try shorter content."
                        : "The AI assistant returned an empty answer.");
            }

            return new AiCompletion(
                content.Trim(),
                finishReason,
                usage.Prompt,
                usage.Completion,
                usage.Reasoning);
        }
        catch (JsonException ex)
        {
            throw new AiException(AiFailure.InvalidResponse, "The AI assistant returned an unreadable answer.", ex);
        }
    }

    /// <summary>
    /// Token accounting, where the engine reports it. Absent fields simply read as zero -- this is
    /// diagnostic information, never a reason to fail a reply that is otherwise fine.
    /// </summary>
    private static (int Prompt, int Completion, int Reasoning) ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return (0, 0, 0);

        static int Int(JsonElement parent, string name) =>
            parent.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;

        var reasoning = usage.TryGetProperty("completion_tokens_details", out var details)
            && details.ValueKind == JsonValueKind.Object
                ? Int(details, "reasoning_tokens")
                : 0;

        return (Int(usage, "prompt_tokens"), Int(usage, "completion_tokens"), reasoning);
    }

    /// <summary>
    /// Builds the pinned handler. Registered from AiServiceRegistration so the pin is applied to
    /// every call made through this client, and to nothing else in the application.
    /// </summary>
    public static HttpMessageHandler CreatePinnedHandler(AiOptions options, ILogger logger)
    {
        var expected = (options.CertificateSha256 ?? string.Empty)
            .Replace(":", "").Replace(" ", "").Trim().ToUpperInvariant();

        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
            {
                if (certificate is null || expected.Length == 0)
                    return false;

                var actual = Convert.ToHexString(SHA256.HashData(certificate.RawData));
                if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    return true;

                logger.LogError(
                    "AI gateway certificate did not match the configured pin. Expected {Expected}, got {Actual}.",
                    expected, actual);
                return false;
            }
        };
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..max];
}
