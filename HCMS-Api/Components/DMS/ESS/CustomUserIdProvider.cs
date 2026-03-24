using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace HCMS_Api.Components.DMS.ESS;

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        // Try to fetch standard claims where the User ID might be stored
        var claim = connection.User?.FindFirst(ClaimTypes.NameIdentifier) 
                    ?? connection.User?.FindFirst("UserEmpID") 
                    ?? connection.User?.FindFirst("UserId")
                    ?? connection.User?.FindFirst("id");

        // Returns the User ID to SignalR's internal mapping dictionary
        return claim?.Value;
    }
}