namespace GraNAS.Notifications.Models.DTO;

public sealed record PagedNotificationsResponse(
    IReadOnlyList<NotificationListItemDto> Items,
    string? NextCursor);
