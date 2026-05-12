using System.Text.Json.Serialization;

namespace GraNAS.Auth.Models.DTO;

public class NotificationPrefsDto
{
    [JsonPropertyName("email")]   public NotificationChannelPrefs Email   { get; set; } = new();
    [JsonPropertyName("inApp")]   public NotificationChannelPrefs InApp   { get; set; } = new();
    [JsonPropertyName("webPush")] public NotificationChannelPrefs WebPush { get; set; } = new();
}
