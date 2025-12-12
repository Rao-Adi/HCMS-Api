using HCMS_Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace HCMS_Api.Services.Authorization
{
    public interface IAuthorization
    {
        Task<string> GenerateToken([FromBody] JwtArray? data);

    }
}
