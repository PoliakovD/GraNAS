using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models;

public class Device
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
