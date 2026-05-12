using System.Text.Json.Serialization;

namespace GraNAS.Auth.Models.DTO;

public class NotificationChannelPrefs
{
    [JsonPropertyName("access_granted")] public bool AccessGranted { get; set; } = true;
    [JsonPropertyName("access_revoked")] public bool AccessRevoked { get; set; } = true;
    [JsonPropertyName("share_revoked")]  public bool ShareRevoked  { get; set; } = true;
    [JsonPropertyName("access_lost")]    public bool AccessLost    { get; set; } = true;
}
