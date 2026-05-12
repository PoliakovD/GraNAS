using System.Text.Json.Serialization;

namespace GraNAS.Auth.Models.DTO;

public class UserSettingsResponse
{
    [JsonPropertyName("notificationPrefs")] public NotificationPrefsDto NotificationPrefs { get; set; } = new();
}
