using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models.DTO;

public class ActiveSessionResponse
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string Ip { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
}
