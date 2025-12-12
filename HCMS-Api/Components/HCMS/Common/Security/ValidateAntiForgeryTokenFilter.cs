using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace HCMS_Api.Components.HCMS.Common.Security
{
    public sealed class ValidateAntiForgeryTokenFilter : ActionFilterAttribute
    {
        private const string XsrfHeader = "XSRF-TOKEN";
        private const string XsrfCookie = "xsrf-token";

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Request.Headers.TryGetValue("Token", out var tokenHeader) &&
                context.HttpContext.Request.Headers.TryGetValue("xsrftoken", out var xsrfTokenHeader))
            {
                string formToken = tokenHeader[0];
                string xsrftoken = xsrfTokenHeader[0];

                var antiforgery = (Microsoft.AspNetCore.Antiforgery.IAntiforgery)context.HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.Antiforgery.IAntiforgery));

                antiforgery.ValidateRequestAsync(context.HttpContext).GetAwaiter().GetResult();
            }
            else
            {
                // Handle missing headers as needed
                context.Result = new BadRequestResult();
            }
        }

        public bool CheckAntiForgeryToken(HttpContext context)
        {
            try
            {
                OnActionExecuting(new ActionExecutingContext(
                    new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
                    new List<IFilterMetadata>(), new Dictionary<string, object>(), controller: null));

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class AntiForgeryTokenModel
    {
        public string Token { get; set; }
        public string xsrftoken { get; set; }
    }
}
