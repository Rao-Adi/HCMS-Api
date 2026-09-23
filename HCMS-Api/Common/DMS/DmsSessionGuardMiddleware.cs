using System.Text.Json;
using HCMS_Api.Components.DMS.Common;

namespace HCMS_Api.Common.DMS;

/// <summary>
/// Refuses a DMS request whose session cannot be resolved, with a message that says so.
///
/// Every DMS component begins with <c>int.Parse(_utilities.GetCompanyId(clientIp))</c> -- about
/// three hundred call sites. GetCompanyId resolves the caller through the Security database and
/// Redis, and it swallows any failure and returns an empty string. So the moment either of those
/// is unreachable -- a dropped VPN, a network blip on the client's own premises -- every one of
/// those call sites throws <c>FormatException: The input string '' was not in a correct
/// format</c>, which reached the user as "Failed to fetch documents. Details: The input string ''
/// was not in a correct format."
///
/// That message says nothing about what happened or what to do, and it appears on whichever
/// screen the user happened to be on, as though that screen were broken.
///
/// Checking once, here, is what makes the answer intelligible: the request stops with a 401 and a
/// sentence about the session, instead of a FormatException from somewhere deep in a query. It
/// also means the three hundred call sites never run with no company at all.
/// </summary>
public sealed class DmsSessionGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DmsSessionGuardMiddleware> _logger;

    public DmsSessionGuardMiddleware(RequestDelegate next, ILogger<DmsSessionGuardMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, DMSUtilities utilities, ClientContextService clientContext)
    {
        var path = context.Request.Path.Value ?? "";

        // Only the DMS API depends on this session lookup. Anything else -- static files, the
        // SignalR handshake, Swagger -- is left alone.
        if (!path.StartsWith("/api/DMS", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var clientIp = clientContext.GetClientIP();

        // No session key at all is a different thing from one that cannot be resolved: the caller
        // simply is not signed in, and saying so is more useful than talking about connectivity.
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            await WriteAsync(context, 401,
                "You are not signed in. Please sign in and try again.");
            return;
        }

        string companyId;
        try
        {
            companyId = utilities.GetCompanyId(clientIp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DMS session lookup failed for {Path}.", path);
            companyId = "";
        }

        if (string.IsNullOrWhiteSpace(companyId) || !int.TryParse(companyId, out _))
        {
            // Logged at warning rather than error: from the application's point of view an
            // unreachable authentication service is an operating condition, not a fault in the
            // screen the user was on.
            _logger.LogWarning(
                "DMS session could not be resolved for {Path}. The Security database or Redis is " +
                "likely unreachable.", path);

            await WriteAsync(context, 401,
                "Your session could not be verified, so this request was not carried out. " +
                "Please try again. If it keeps happening, the connection to the authentication " +
                "service is down -- nothing you were working on has been lost.");
            return;
        }

        await _next(context);
    }

    private static async Task WriteAsync(HttpContext context, int status, string message)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        // The same envelope every DMS endpoint returns, so the client handles it as it would any
        // other refusal rather than as a transport error.
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            Data = new { },
            Success = false,
            Message = message,
            Code = status,
        }));
    }
}
