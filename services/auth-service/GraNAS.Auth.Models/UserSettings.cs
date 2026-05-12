namespace GraNAS.Auth.Models;

public class UserSettings
{
    public Guid UserId { get; set; }
    public string NotificationPrefsJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
