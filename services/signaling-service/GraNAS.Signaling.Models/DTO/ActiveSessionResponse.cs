using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models.DTO;

/// <summary>
/// Информация об активной онлайн-сессии устройства пользователя.
/// Возвращается эндпоинтом <c>GET /api/sessions</c>.
/// </summary>
public class ActiveSessionResponse
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string Ip { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
}
