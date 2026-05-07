using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Notifications.Models.DTO;
using GraNAS.Notifications.Models.Repositories;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GraNAS.Notifications.API.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationEventRepository _events;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationEventRepository events,
        ILogger<NotificationsController> logger)
    {
        _events = events;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedNotificationsResponse>> List(
        [FromQuery] string? cursor,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        DateTime? cursorDate = null;
        if (!string.IsNullOrEmpty(cursor) && DateTime.TryParse(cursor, out var parsed))
            cursorDate = parsed;

        take = Math.Clamp(take, 1, 100);
        var items = await _events.ListByUserAsync(userId, cursorDate, take + 1, ct);

        string? nextCursor = null;
        IReadOnlyList<NotificationListItemDto> result;

        if (items.Count > take)
        {
            nextCursor = items[take - 1].CreatedAt.ToString("O");
            result = items.Take(take)
                .Select(e => new NotificationListItemDto(e.Id, e.Type, e.Data.RootElement, e.IsRead, e.CreatedAt))
                .ToList();
        }
        else
        {
            result = items
                .Select(e => new NotificationListItemDto(e.Id, e.Type, e.Data.RootElement, e.IsRead, e.CreatedAt))
                .ToList();
        }

        _logger.LogDebug("List notifications for user {UserId}: {Count} items", userId, result.Count);
        return Ok(new PagedNotificationsResponse(result, nextCursor));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var count = await _events.CountUnreadAsync(userId, ct);
        _logger.LogDebug("UnreadCount for user {UserId}: {Count}", userId, count);
        return Ok(new UnreadCountResponse(count));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var marked = await _events.MarkReadAsync(id, userId, ct);
        if (!marked)
        {
            _logger.LogWarning("MarkRead: notification {NotificationId} not found for current user", id);
            return NotFound(new ErrorResponse { Error = "not_found", ErrorDescription = "Notification not found." });
        }

        _logger.LogInformation("Notification {NotificationId} marked read", id);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var count = await _events.MarkAllReadAsync(userId, ct);
        _logger.LogInformation("All notifications marked read for current user (count={Count})", count);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
