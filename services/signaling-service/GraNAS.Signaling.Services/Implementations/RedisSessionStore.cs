using System.Text.Json;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GraNAS.Signaling.Services.Implementations;

/// <summary>
/// Реализация <see cref="ISessionStore"/> на базе Redis.
/// Хранит исключительно эфемерное состояние (TTL 24 ч для устройств, 1 ч для P2P-пар);
/// при перезапуске устройство должно заново вызвать <c>RegisterDevice</c> в SignalR-хабе.
/// </summary>
/// <remarks>
/// Схема ключей Redis:
/// <list type="bullet">
/// <item><c>signaling:conn:{deviceId}</c> — string: текущий SignalR connectionId устройства</item>
/// <item><c>signaling:device:{connId}</c> — string: deviceId по connectionId</item>
/// <item><c>signaling:user-sessions:{userId}</c> — set: deviceId всех онлайн-устройств пользователя</item>
/// <item><c>signaling:session:{deviceId}</c> — string JSON: <see cref="SessionInfo"/> (IP, время подключения)</item>
/// <item><c>signaling:folder-owners:{folderId}</c> — set: deviceId онлайн-owner'ов папки</item>
/// <item><c>signaling:pair:{connId}</c> — set: connectionId партнёра по P2P-сессии</item>
/// </list>
/// </remarks>
public class RedisSessionStore : ISessionStore
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisSessionStore> _logger;
    private static readonly TimeSpan DeviceTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(1);

    public RedisSessionStore(IConnectionMultiplexer redis, ILogger<RedisSessionStore> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    // ── Key builders ──────────────────────────────────────────────────────────
    private static string ConnKey(Guid deviceId) => $"signaling:conn:{deviceId}";
    private static string DeviceKey(string connectionId) => $"signaling:device:{connectionId}";
    private static string UserSessionsKey(Guid userId) => $"signaling:user-sessions:{userId}";
    private static string SessionInfoKey(Guid deviceId) => $"signaling:session:{deviceId}";
    private static string FolderOwnersKey(Guid folderId) => $"signaling:folder-owners:{folderId}";
    private static string PairKey(string connId) => $"signaling:pair:{connId}";

    // ── Device ↔ Connection ───────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>Все записи сохраняются атомарным <c>batch</c>; TTL каждого ключа — 24 ч.</remarks>
    public async Task RegisterDeviceConnectionAsync(Guid deviceId, string connectionId, Guid userId, string ip, CancellationToken ct = default)
    {
        var info = new SessionInfo
        {
            ConnectionId = connectionId,
            Ip = ip,
            ConnectedAt = DateTime.UtcNow,
            UserId = userId
        };
        var json = JsonSerializer.Serialize(info);

        var batch = _db.CreateBatch();
        var t1 = batch.StringSetAsync(ConnKey(deviceId), connectionId, DeviceTtl);
        var t2 = batch.StringSetAsync(DeviceKey(connectionId), deviceId.ToString(), DeviceTtl);
        var t3 = batch.StringSetAsync(SessionInfoKey(deviceId), json, DeviceTtl);
        var t4 = batch.SetAddAsync(UserSessionsKey(userId), deviceId.ToString());
        var t5 = batch.KeyExpireAsync(UserSessionsKey(userId), DeviceTtl);
        batch.Execute();
        await Task.WhenAll(t1, t2, t3, t4, t5);
    }

    /// <inheritdoc/>
    public async Task RemoveDeviceConnectionAsync(Guid deviceId, string connectionId, Guid userId, CancellationToken ct = default)
    {
        var current = (string?)await _db.StringGetAsync(ConnKey(deviceId));
        if (current != connectionId)
        {
            _logger.LogDebug("Remove device connection: device {DeviceId} stale call — connection already replaced", deviceId);
            return; // stale call — another connection already registered
        }

        var batch = _db.CreateBatch();
        var t1 = batch.KeyDeleteAsync(ConnKey(deviceId));
        var t2 = batch.KeyDeleteAsync(DeviceKey(connectionId));
        var t3 = batch.KeyDeleteAsync(SessionInfoKey(deviceId));
        var t4 = batch.SetRemoveAsync(UserSessionsKey(userId), deviceId.ToString());
        batch.Execute();
        await Task.WhenAll(t1, t2, t3, t4);
    }

    /// <inheritdoc/>
    public async Task<string?> GetConnectionIdByDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(ConnKey(deviceId));
        return val.HasValue ? (string?)val : null;
    }

    /// <inheritdoc/>
    public async Task<Guid?> GetDeviceIdByConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(DeviceKey(connectionId));
        return val.HasValue && Guid.TryParse((string?)val, out var id) ? id : null;
    }

    /// <inheritdoc/>
    public Task<bool> IsDeviceOnlineAsync(Guid deviceId, CancellationToken ct = default)
        => _db.KeyExistsAsync(ConnKey(deviceId));

    /// <inheritdoc/>
    public async Task<List<Guid>> GetOnlineDevicesByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var members = await _db.SetMembersAsync(UserSessionsKey(userId));
        return members
            .Where(m => m.HasValue)
            .Select(m => Guid.TryParse((string?)m, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<SessionInfo?> GetSessionInfoAsync(Guid deviceId, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(SessionInfoKey(deviceId));
        if (!val.HasValue) return null;
        return JsonSerializer.Deserialize<SessionInfo>((string)val!);
    }

    // ── Folder owner tracking ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task RegisterOwnerAsync(Guid folderId, Guid deviceId, CancellationToken ct = default)
    {
        await _db.SetAddAsync(FolderOwnersKey(folderId), deviceId.ToString());
        await _db.KeyExpireAsync(FolderOwnersKey(folderId), DeviceTtl);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveOwnerAsync(Guid folderId, Guid deviceId, CancellationToken ct = default)
    {
        await _db.SetRemoveAsync(FolderOwnersKey(folderId), deviceId.ToString());
        var remaining = await _db.SetLengthAsync(FolderOwnersKey(folderId));
        return remaining == 0;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Использует <c>SetRandomMember</c> для выбора одного из зарегистрированных owner'ов.
    /// Если у выбранного устройства нет активного соединения — удаляет устаревшую запись (lazy cleanup)
    /// и возвращает <c>null</c>, не повторяя попытку.
    /// </remarks>
    public async Task<Guid?> GetOwnerDeviceIdAsync(Guid folderId, CancellationToken ct = default)
    {
        var val = await _db.SetRandomMemberAsync(FolderOwnersKey(folderId));
        if (!val.HasValue || !Guid.TryParse((string?)val, out var deviceId)) return null;

        // Ленивая очистка: если соединение уже закрыто — удаляем устаревшую запись
        var connId = await GetConnectionIdByDeviceAsync(deviceId, ct);
        if (connId is null)
        {
            await RemoveOwnerAsync(folderId, deviceId, ct);
            return null;
        }

        return deviceId;
    }

    // ── P2P session pairs ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Регистрация симметрична: каждый участник добавляется в pair-set другого.
    /// TTL обоих ключей — 1 ч. Пара используется в <c>AssertValidSessionAsync</c>
    /// для проверки, что SDP/ICE пересылается только между легитимными участниками сессии.
    /// </remarks>
    public async Task RegisterSessionPairAsync(string receiverConnId, string ownerConnId, Guid folderId, CancellationToken ct = default)
    {
        var batch = _db.CreateBatch();
        var t1 = batch.SetAddAsync(PairKey(receiverConnId), ownerConnId);
        var t2 = batch.KeyExpireAsync(PairKey(receiverConnId), SessionTtl);
        var t3 = batch.SetAddAsync(PairKey(ownerConnId), receiverConnId);
        var t4 = batch.KeyExpireAsync(PairKey(ownerConnId), SessionTtl);
        batch.Execute();
        await Task.WhenAll(t1, t2, t3, t4);
        _logger.LogDebug("Session pair registered: {ConnA} ↔ {ConnB} for folder {FolderId}",
            receiverConnId, ownerConnId, folderId);
    }

    /// <inheritdoc/>
    public Task<bool> IsValidSessionPairAsync(string connA, string connB, CancellationToken ct = default)
        => _db.SetContainsAsync(PairKey(connA), connB);

    /// <inheritdoc/>
    public async Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        var peers = await _db.SetMembersAsync(PairKey(connectionId));
        foreach (var peer in peers)
            await _db.SetRemoveAsync(PairKey(peer!), connectionId);
        await _db.KeyDeleteAsync(PairKey(connectionId));
    }
}
