namespace GraNAS.Signaling.Models;

/// <summary>
/// Привязка папки к конкретному устройству (device-folder binding).
/// Каждая папка может быть привязана ровно к одному устройству; привязка устанавливается
/// явно через <c>Claim</c> — не автоматически при подключении к хабу.
/// </summary>
public class DeviceFolder
{
    /// <summary>
    /// Идентификатор папки (первичный ключ таблицы). Одна папка — одно устройство.
    /// Совпадает с идентификатором папки в metadata-service.
    /// </summary>
    public Guid FolderId { get; set; }
    public Guid DeviceId { get; set; }
    /// <summary>Момент последней успешной привязки папки к устройству.</summary>
    public DateTime ClaimedAt { get; set; }
    public Device Device { get; set; } = null!;
}
