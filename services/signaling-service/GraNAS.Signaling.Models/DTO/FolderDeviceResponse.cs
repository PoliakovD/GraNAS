using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models.DTO;

public class FolderDeviceResponse
{
    public Guid FolderId { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public bool IsOnline { get; set; }
    public DateTime ClaimedAt { get; set; }
}
