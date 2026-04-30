using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models.DTO;

public class DeviceResponse
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsOnline { get; set; }
}
