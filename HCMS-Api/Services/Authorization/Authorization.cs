using HCMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
namespace HCMS_Api.Services.Authorization
{
    public class Authorization : IAuthorization
    {
        private readonly TokenService _tokenService;
        private readonly Common.Common _common;
        public Authorization(TokenService tokenService, Common.Common common)
        {
            _tokenService = tokenService;
            _common = common;
        }

        public async Task<string> GenerateToken([FromBody] JwtArray? data)
        {
            try
            {
                var token = _tokenService.GenerateToken(data);
                return token;
            }


            catch (Exception ex)
            {
                throw new Exception();
            }
        }
    }
}
