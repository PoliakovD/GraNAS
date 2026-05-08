namespace GraNAS.Signaling.Models;

public class DeviceFolder
{
    public Guid FolderId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime ClaimedAt { get; set; }
    public Device Device { get; set; } = null!;
}
