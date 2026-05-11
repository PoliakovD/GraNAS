namespace GraNAS.Signaling.Models.Repositories;

/// <summary>Репозиторий привязок «папка ↔ устройство» (device-folder binding).</summary>
public interface IDeviceFolderRepository
{
    /// <summary>Возвращает привязку по идентификатору папки или <c>null</c>, если папка не занята ни одним устройством.</summary>
    Task<DeviceFolder?> GetByFolderIdAsync(Guid folderId, CancellationToken ct = default);
    /// <summary>
    /// Атомарно привязывает папку к устройству.
    /// Если папка уже занята другим устройством того же пользователя — перезаписывает привязку
    /// (реализовано через <c>INSERT … ON CONFLICT (folder_id) DO UPDATE</c>).
    /// </summary>
    Task ClaimAsync(Guid deviceId, Guid folderId, CancellationToken ct = default);
    /// <summary>Освобождает привязку папки от устройства. Если привязки нет — операция выполняется без ошибок.</summary>
    Task ReleaseAsync(Guid deviceId, Guid folderId, CancellationToken ct = default);
    /// <summary>
    /// Возвращает привязки для указанного набора папок, принадлежащих заданному пользователю.
    /// Папки без привязки в результат не включаются.
    /// </summary>
    Task<List<DeviceFolder>> GetByFolderIdsAsync(IEnumerable<Guid> folderIds, Guid userId, CancellationToken ct = default);
    /// <summary>Удаляет все привязки папок к устройству. Используется при удалении или переносе устройства.</summary>
    Task RemoveAllByDeviceAsync(Guid deviceId, CancellationToken ct = default);
}
