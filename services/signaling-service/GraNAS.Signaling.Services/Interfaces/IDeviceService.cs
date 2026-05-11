using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.DTO;

namespace GraNAS.Signaling.Services.Interfaces;

/// <summary>Сервис управления устройствами пользователей и их привязками к папкам.</summary>
public interface IDeviceService
{
    /// <summary>
    /// Регистрирует новое устройство или обновляет имя и платформу существующего.
    /// После сохранения обновляет <c>LastSeenAt</c> и проверяет онлайн-статус по Redis.
    /// </summary>
    Task<DeviceResponse> RegisterOrUpdateAsync(Guid userId, DeviceRegistrationRequest req, CancellationToken ct = default);

    /// <summary>
    /// Возвращает все устройства пользователя с актуальным онлайн-статусом из Redis.
    /// </summary>
    Task<List<DeviceResponse>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Проверяет, принадлежит ли устройство указанному пользователю.</summary>
    Task<bool> BelongsToUserAsync(Guid deviceId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Пытается привязать папку к устройству.
    /// Без флага <paramref name="force"/>: если папка уже занята другим устройством того же пользователя —
    /// возвращает существующую привязку (конфликт).
    /// С флагом <paramref name="force"/>: перезаписывает привязку атомарно через <c>ON CONFLICT DO UPDATE</c>.
    /// </summary>
    /// <param name="force">Принудительно перепривязать папку, игнорируя текущего владельца.</param>
    /// <returns><c>null</c> при успехе; существующий <see cref="DeviceFolder"/> при конфликте (только без <c>force</c>).</returns>
    Task<DeviceFolder?> TryClaimFolderAsync(Guid deviceId, Guid folderId, bool force, CancellationToken ct = default);

    /// <summary>Освобождает привязку папки от устройства.</summary>
    Task ReleaseFolderAsync(Guid deviceId, Guid folderId, CancellationToken ct = default);

    /// <summary>
    /// Возвращает информацию о том, какое устройство держит каждую из запрошенных папок.
    /// Папки без привязки в результат не включаются. Онлайн-статус дополняется из Redis.
    /// </summary>
    Task<List<FolderDeviceResponse>> GetFolderDevicesAsync(IEnumerable<Guid> folderIds, Guid userId, CancellationToken ct = default);
}
