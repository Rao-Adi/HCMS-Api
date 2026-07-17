using HCMS_Api.Common;
using HCMS_Api.Components.DMS.Common;
using Microsoft.AspNetCore.SignalR;

namespace HCMS_Api.Components.DMS.ESS;

//[Authorize]
public class NotificationHub : Hub
{
    private readonly NotificationComponent _notificationComponent;
    private readonly DMSUtilities _utilities;
    private readonly ClientContextService _clientContextService;

    public NotificationHub(
        NotificationComponent notificationComponent,
        DMSUtilities utilities,
        ClientContextService clientContextService)
    {
        _notificationComponent = notificationComponent;
        _utilities = utilities;
        _clientContextService = clientContextService;
    }

    private string? GetUserEmpCode()
    {
        try
        {
            var clientIp = _clientContextService.GetClientIP();
            if (string.IsNullOrEmpty(clientIp))
            {
                Console.WriteLine("[SIGNALR] GetUserEmpCode: clientIp is null or empty.");
                return null;
            }

            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            Console.WriteLine($"[SIGNALR] GetUserEmpCode: Resolved clientIp='{clientIp}', empId='{empId}', empCode='{empCode}'");
            return empCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SIGNALR] GetUserEmpCode Exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Called when a new client connects to the hub.
    /// It automatically adds the user to a unique, private group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var empCode = GetUserEmpCode();
        if (!string.IsNullOrEmpty(empCode))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{empCode}");
            Console.WriteLine($"[SIGNALR] Connection {Context.ConnectionId} successfully mapped to user_{empCode} on connect.");
        }
        else
        {
            Console.WriteLine($"[SIGNALR] Connection {Context.ConnectionId} connected anonymously (could not resolve user from context headers/query).");
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects.
    /// It automatically cleans up by removing the connection from its group.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var empCode = GetUserEmpCode();
        if (!string.IsNullOrEmpty(empCode))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{empCode}");
            Console.WriteLine($"[SIGNALR] Connection {Context.ConnectionId} removed from user_{empCode} on disconnect.");
        }
        else
        {
            Console.WriteLine($"[SIGNALR] Connection {Context.ConnectionId} disconnected (was anonymous).");
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Explicitly registers the client connection to a user group using a provided login identifier.
    /// This acts as a reliable fallback if header/query parsing fails during initial handshake.
    /// </summary>
    public async Task RegisterClient(string loginId)
    {
        try
        {
            if (string.IsNullOrEmpty(loginId))
            {
                Console.WriteLine($"[SIGNALR] RegisterClient called with empty loginId for connection {Context.ConnectionId}");
                return;
            }

            var empId = _utilities.GetEmpid(loginId);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (!string.IsNullOrEmpty(empCode))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{empCode}");
                Console.WriteLine($"[SIGNALR] Connection {Context.ConnectionId} successfully registered to user_{empCode} via RegisterClient using loginId: '{loginId}'");
            }
            else
            {
                Console.WriteLine($"[SIGNALR] RegisterClient: Could not resolve employee code for loginId: '{loginId}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SIGNALR] Error in RegisterClient for connection {Context.ConnectionId}: {ex.Message}");
        }
    }

    public async Task SendTestNotification(string title, string message, string type)
    {
        var notification = new
        {
            id = 0, // Mock ID for testing
            title = title,
            message = message,
            type = type
        };

        // Broadcast to EVERY connected client for testing purposes.
        await Clients.All.SendAsync("ReceiveNotification", notification);
    }

    /// <summary>
    /// Allows a client to mark a specific notification as read.
    /// </summary>
    /// <param name="notificationId">The ID of the notification to mark as read.</param>
    public async Task MarkAsRead(int notificationId)
    {
        await _notificationComponent.MarkAsReadAsync(notificationId);
    }

    /// <summary>
    /// Allows a client to mark all their unread notifications as read.
    /// </summary>
    public async Task MarkAllAsRead()
    {
        await _notificationComponent.MarkAllAsReadAsync();
    }

    /// <summary>
    /// Explicitly joins the user's notification group. Useful for client-side logic after initial connection.
    /// </summary>
    public async Task JoinUserGroup()
    {
        var empCode = GetUserEmpCode();
        if (!string.IsNullOrEmpty(empCode))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{empCode}");
            Console.WriteLine($"[SIGNALR] Connection {Context.ConnectionId} joined user_{empCode} via JoinUserGroup.");
        }
        else
        {
            Console.WriteLine($"[SIGNALR] JoinUserGroup: Could not resolve user from context.");
        }
    }
}