using System.ComponentModel.DataAnnotations;

namespace GraNAS.Signaling.Models.DTO;

/// <summary>Запрос переименования устройства.</summary>
public class DeviceRenameRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string DeviceName { get; set; } = string.Empty;
}
