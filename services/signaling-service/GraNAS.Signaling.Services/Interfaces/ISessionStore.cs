using GraNAS.Signaling.Models.DTO;

namespace GraNAS.Signaling.Services.Interfaces;

/// <summary>
/// Хранилище эфемерного состояния сигналинга в Redis.
/// Все данные хранятся без персистентности; после перезапуска устройство должно переподключиться.
/// </summary>
public interface ISessionStore
{
    // ── Device ↔ Connection mapping ─────────────────────────────────────────

    /// <summary>
    /// Регистрирует подключение устройства: сохраняет двунаправленный маппинг
    /// <c>deviceId ↔ connectionId</c>, добавляет устройство в список онлайн-устройств пользователя
    /// и сохраняет полную информацию о сессии (<see cref="SessionInfo"/>).
    /// </summary>
    Task RegisterDeviceConnectionAsync(Guid deviceId, string connectionId, Guid userId, string ip, CancellationToken ct = default);

    /// <summary>
    /// Удаляет записи о подключении устройства.
    /// Если <paramref name="connectionId"/> не совпадает с текущим зарегистрированным — операция игнорируется
    /// (защита от гонки при переподключении).
    /// </summary>
    Task RemoveDeviceConnectionAsync(Guid deviceId, string connectionId, Guid userId, CancellationToken ct = default);

    /// <summary>Возвращает текущий SignalR <c>connectionId</c> устройства или <c>null</c>, если устройство офлайн.</summary>
    Task<string?> GetConnectionIdByDeviceAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>Возвращает идентификатор устройства по SignalR <c>connectionId</c> или <c>null</c>, если соединение не найдено.</summary>
    Task<Guid?> GetDeviceIdByConnectionAsync(string connectionId, CancellationToken ct = default);

    /// <summary>Проверяет, подключено ли устройство к хабу в данный момент.</summary>
    Task<bool> IsDeviceOnlineAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>Возвращает список идентификаторов устройств пользователя, находящихся онлайн.</summary>
    Task<List<Guid>> GetOnlineDevicesByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Возвращает подробную информацию о сессии устройства или <c>null</c>, если устройство офлайн.</summary>
    Task<SessionInfo?> GetSessionInfoAsync(Guid deviceId, CancellationToken ct = default);

    // ── Folder owner tracking (by deviceId) ─────────────────────────────────

    /// <summary>Добавляет устройство в множество онлайн-owner'ов папки.</summary>
    Task RegisterOwnerAsync(Guid folderId, Guid deviceId, CancellationToken ct = default);

    /// <summary>Удаляет устройство из множества онлайн-owner'ов папки.</summary>
    /// <returns><c>true</c>, если после удаления ни одного owner'а не осталось онлайн.</returns>
    Task<bool> RemoveOwnerAsync(Guid folderId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// Возвращает идентификатор устройства, которое сейчас онлайн и является owner'ом папки,
    /// или <c>null</c>, если ни один owner офлайн.
    /// При обнаружении устаревших записей (устройство отключилось) выполняет ленивую очистку.
    /// </summary>
    Task<Guid?> GetOwnerDeviceIdAsync(Guid folderId, CancellationToken ct = default);

    // ── P2P session pairs ────────────────────────────────────────────────────

    /// <summary>
    /// Регистрирует сессионную пару <c>(receiverConnId, ownerConnId)</c> после успешного
    /// <c>RequestSession</c>. Пара валидирует право на relay SDP/ICE между участниками.
    /// TTL пары — 1 час.
    /// </summary>
    Task RegisterSessionPairAsync(string receiverConnId, string ownerConnId, Guid folderId, CancellationToken ct = default);

    /// <summary>
    /// Проверяет, образуют ли два соединения зарегистрированную сессионную пару.
    /// Используется в <c>AssertValidSessionAsync</c> перед relay SDP/ICE.
    /// </summary>
    Task<bool> IsValidSessionPairAsync(string connA, string connB, CancellationToken ct = default);

    /// <summary>Удаляет все данные сессионной пары для указанного соединения при отключении.</summary>
    Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default);
}
