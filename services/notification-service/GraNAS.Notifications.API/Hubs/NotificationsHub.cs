using System;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Notifications.Models.DTO;
using GraNAS.Notifications.Models.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace GraNAS.Notifications.API.Hubs;

[Authorize]
public class NotificationsHub : Hub
{
    private readonly INotificationEventRepository _events;
    private readonly ILogger<NotificationsHub> _logger;

    public NotificationsHub(INotificationEventRepository events, ILogger<NotificationsHub> logger)
    {
        _events = events;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        if (!TryGetUserId(out var userId))
        {
            _logger.LogWarning("Notifications hub: connection refused — unauthenticated");
            Context.Abort();
            return;
        }

        using (LogContext.PushProperty("UserId", userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            _logger.LogInformation("Notifications hub: connected user {UserId} conn={ConnectionId}",
                userId, Context.ConnectionId);

            var unread = await _events.GetUnreadByUserAsync(userId);
            foreach (var evt in unread)
            {
                var dto = new NotificationListItemDto(
                    evt.Id, evt.Type, evt.Data.RootElement, evt.IsRead, evt.CreatedAt);
                await Clients.Caller.SendAsync("NotificationReceived", dto);
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task MarkRead(Guid notificationId)
    {
        if (!TryGetUserId(out var userId)) return;

        using (LogContext.PushProperty("UserId", userId))
        {
            var marked = await _events.MarkReadAsync(notificationId, userId);
            if (marked)
            {
                _logger.LogInformation("Notifications hub: marked read {NotificationId}", notificationId);
                await Clients.Group($"user-{userId}").SendAsync("NotificationRead", notificationId);
            }
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }
}
