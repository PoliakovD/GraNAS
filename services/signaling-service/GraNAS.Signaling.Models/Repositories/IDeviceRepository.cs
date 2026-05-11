namespace GraNAS.Signaling.Models.Repositories;

/// <summary>Репозиторий устройств пользователей.</summary>
public interface IDeviceRepository
{
    /// <summary>Возвращает устройство по идентификатору или <c>null</c>, если устройство не найдено.</summary>
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Возвращает все устройства, зарегистрированные указанным пользователем.</summary>
    Task<List<Device>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Создаёт устройство или обновляет его имя и платформу, если оно уже существует.</summary>
    /// <returns>Сохранённая сущность устройства.</returns>
    /// <exception cref="InvalidOperationException">Если устройство с таким <c>Id</c> принадлежит другому пользователю.</exception>
    Task<Device> UpsertAsync(Device device, CancellationToken ct = default);
    /// <summary>Проверяет, принадлежит ли устройство указанному пользователю.</summary>
    Task<bool> BelongsToUserAsync(Guid deviceId, Guid userId, CancellationToken ct = default);
    /// <summary>Обновляет <see cref="Device.LastSeenAt"/> без загрузки сущности в память (<c>ExecuteUpdateAsync</c>).</summary>
    Task TouchLastSeenAsync(Guid id, CancellationToken ct = default);
}
