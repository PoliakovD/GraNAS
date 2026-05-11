using System.ComponentModel.DataAnnotations;
using GraNAS.Signaling.Models.Enums;

namespace GraNAS.Signaling.Models.DTO;

/// <summary>Запрос на регистрацию или обновление устройства в системе сигналинга.</summary>
public class DeviceRegistrationRequest
{
    /// <summary>
    /// Идентификатор устройства, сгенерированный на стороне клиента.
    /// На desktop хранится в Windows Credential Manager (<c>GraNAS:deviceId</c>).
    /// </summary>
    [Required]
    public Guid DeviceId { get; set; }

    [Required, MaxLength(100)]
    public string DeviceName { get; set; } = string.Empty;

    [Required]
    public DevicePlatform Platform { get; set; }
}
