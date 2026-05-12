using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GraNAS.Auth.Models.DTO;

public class UpdateUserSettingsRequest
{
    [Required]
    [JsonPropertyName("notificationPrefs")]
    public NotificationPrefsDto NotificationPrefs { get; set; } = null!;
}
