using System.Text.Json;

namespace GraNAS.Notifications.Models.DTO;

public sealed record NotificationListItemDto(
    Guid Id,
    string Type,
    JsonElement Data,
    bool IsRead,
    DateTime CreatedAt);
