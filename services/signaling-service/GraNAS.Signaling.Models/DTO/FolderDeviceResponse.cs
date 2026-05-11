using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models.DTO;

/// <summary>
/// Ответ на батч-запрос «какое устройство держит данную папку».
/// Возвращается эндпоинтом <c>GET /api/devices/folder-devices</c> и при конфликте привязки (HTTP 409).
/// </summary>
public class FolderDeviceResponse
{
    public Guid FolderId { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    /// <summary><c>true</c>, если устройство в данный момент подключено к SignalR-хабу.</summary>
    public bool IsOnline { get; set; }
    /// <summary>Момент последней успешной привязки этой папки к устройству.</summary>
    public DateTime ClaimedAt { get; set; }
}
