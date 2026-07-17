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
    }
}
