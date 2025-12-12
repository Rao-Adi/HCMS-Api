using HCMS_Api.Models;
using HCMS_Api.Services.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Configuration;

namespace HCMS_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthorizationController : ControllerBase
    {
        private readonly IAuthorization _authorization;
        private readonly IConfiguration _configuration;
        public AuthorizationController(IAuthorization authorization, IConfiguration configuration)
        {
            _authorization = authorization;
            _configuration = configuration;
        }



        [HttpPost("generatetoken")]
        public async Task<IActionResult> GenerateToken([FromBody] JwtArray? body, string SecKey)
        {
            if (String.IsNullOrWhiteSpace(SecKey) || SecKey != _configuration["Jwt:Key"])
            {
                return Unauthorized("Invalid Reuqest");
            }
            string data = await _authorization.GenerateToken(body);
            return Ok(data);
        }
    }
}
