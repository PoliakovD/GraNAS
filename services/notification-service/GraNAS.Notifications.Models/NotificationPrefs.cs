using System.Text.Json.Serialization;

namespace GraNAS.Notifications.Models;

public class NotificationPrefs
{
    [JsonPropertyName("email")]   public NotificationChannelPrefs Email   { get; set; } = new(true);
    [JsonPropertyName("inApp")]   public NotificationChannelPrefs InApp   { get; set; } = new(true);
    [JsonPropertyName("webPush")] public NotificationChannelPrefs WebPush { get; set; } = new(false);

    public static NotificationPrefs AllEnabled() => new();

    public bool IsEnabled(DeliveryTarget target, string eventType) => target switch
    {
        DeliveryTarget.Email   => Email.IsEnabled(eventType),
        DeliveryTarget.SignalR => InApp.IsEnabled(eventType),
        DeliveryTarget.WebPush => WebPush.IsEnabled(eventType),
        _ => true
    };
}

public class NotificationChannelPrefs
{
    public NotificationChannelPrefs() { }

    public NotificationChannelPrefs(bool defaultValue)
    {
        AccessGranted = defaultValue;
        AccessRevoked = defaultValue;
        ShareRevoked  = defaultValue;
        AccessLost    = defaultValue;
    }

    [JsonPropertyName("access_granted")] public bool AccessGranted { get; set; } = true;
    [JsonPropertyName("access_revoked")] public bool AccessRevoked { get; set; } = true;
    [JsonPropertyName("share_revoked")]  public bool ShareRevoked  { get; set; } = true;
    [JsonPropertyName("access_lost")]    public bool AccessLost    { get; set; } = true;

    public bool IsEnabled(string eventType) => eventType switch
    {
        "access.granted" => AccessGranted,
        "access.revoked" => AccessRevoked,
        "share.revoked"  => ShareRevoked,
        "access.lost"    => AccessLost,
        _ => true
    };
}
