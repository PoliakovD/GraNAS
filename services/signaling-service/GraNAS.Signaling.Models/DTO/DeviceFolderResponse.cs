namespace GraNAS.Signaling.Models.DTO;

/// <summary>
/// Запись о папке, привязанной к конкретному устройству.
/// Возвращается эндпоинтом <c>GET /api/devices/{deviceId}/folders</c>.
/// </summary>
public class DeviceFolderResponse
{
    public Guid FolderId { get; set; }
    public DateTime ClaimedAt { get; set; }
}
