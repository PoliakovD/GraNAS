using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models;

/// <summary>
/// Устройство пользователя, зарегистрированное в системе сигналинга.
/// Идентификатор устройства генерируется на стороне клиента и сохраняется локально
/// (на desktop — Windows Credential Manager, ключ <c>GraNAS:deviceId</c>).
/// </summary>
public class Device
{
    /// <summary>Уникальный идентификатор устройства, сгенерированный клиентом. Пара <c>(UserId, DeviceName)</c> уникальна в БД.</summary>
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Время последней активности устройства. Обновляется при каждом подключении к хабу.</summary>
    public DateTime LastSeenAt { get; set; }
}
