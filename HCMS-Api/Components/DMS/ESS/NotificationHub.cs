using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HCMS_Api.Components.DMS.ESS;

//[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Optional: You can log user connections or add them to specific groups here
        // string userId = Context.UserIdentifier;
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Optional: Handle cleanup on disconnect
        await base.OnDisconnectedAsync(exception);
    }

    // Allows the frontend to manually register their Employee Code to a Group.
    // Useful if standard JWT claims are missing via IUserIdProvider on WebSocket connections.
    public async Task RegisterUser(string empCode)
    {
        if (!string.IsNullOrWhiteSpace(empCode))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, empCode.Trim());
        }
    }

    // 1. Receives the payload from the Test Button in Angular
    public async Task SendTestNotification(string title, string message, string type)
    {
        // 2. Wrap it into an object matching your AppNotification interface
        var notification = new
        {
            title = title,
            message = message,
            type = type
        };

        // 3. Broadcast to EVERY connected client (all tabs / browsers)
        await Clients.All.SendAsync("ReceiveNotification", notification);
    }
}