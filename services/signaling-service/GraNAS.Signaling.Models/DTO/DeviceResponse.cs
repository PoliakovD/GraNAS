using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models.DTO;

/// <summary>Информация об устройстве пользователя, возвращаемая API.</summary>
public class DeviceResponse
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    /// <summary>
    /// <c>true</c>, если устройство в данный момент подключено к SignalR-хабу.
    /// Вычисляется по наличию активной записи в Redis (<c>signaling:conn:{deviceId}</c>).
    /// </summary>
    public bool IsOnline { get; set; }
}
