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
            var headers = _httpContextAccessor.HttpContext?.Request?.Headers;
            if (headers != null && headers.TryGetValue("login", out var loginValues))
            {
                return loginValues.FirstOrDefault() ?? "";
            }
            return "";
        }
    }
}
