using System.ComponentModel.DataAnnotations;
using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models.DTO;

public class DeviceRegistrationRequest
{
    [Required]
    public Guid DeviceId { get; set; }

    [Required, MaxLength(100)]
    public string DeviceName { get; set; } = string.Empty;

    [Required]
    public DevicePlatform Platform { get; set; }
}
