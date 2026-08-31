namespace HCMS_Api.Common
{
    public class ClientContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ClientContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetClientIP()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return "";

            // 1. Check HTTP Headers (used by standard HTTP API calls)
            var headers = httpContext.Request?.Headers;
            if (headers != null && headers.TryGetValue("login", out var loginValues))
            {
                var val = loginValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            // 2. Check Query String (used by WebSockets / SignalR handshake)
            var query = httpContext.Request?.Query;
            if (query != null && query.TryGetValue("login", out var queryValues))
            {
                var val = queryValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            // 3. Fallback to access_token query parameter (standard for SignalR client configurations)
            if (query != null && query.TryGetValue("access_token", out var tokenValues))
            {
                var val = tokenValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            return "";
        }

        // GetClientIP() above returns the "login" session-key header/token despite its name --
        // it's used everywhere else in this codebase for session/company/employee resolution,
        // not as a network address, so it's left alone. This is the actual client network IP,
        // for audit logging. Checks X-Forwarded-For first since this API sits behind a reverse
        // proxy/load balancer in production, where Connection.RemoteIpAddress would otherwise
        // just be the proxy's own address; X-Forwarded-For can carry a comma-separated chain
        // (client, proxy1, proxy2, ...) when there are multiple hops, so the first entry is the
        // original client.
        public string GetRequestIpAddress()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return "";

            var forwardedFor = httpContext.Request?.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstHop = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstHop)) return firstHop;
            }

            return httpContext.Connection?.RemoteIpAddress?.ToString() ?? "";
        }
    }
}
