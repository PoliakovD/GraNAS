using System.Text.Json;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Interfaces;
using StackExchange.Redis;

namespace GraNAS.Signaling.Services.Implementations;

public class RedisSessionStore : ISessionStore
{
    private readonly IDatabase _db;
    private static readonly TimeSpan DeviceTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(1);

    public RedisSessionStore(IConnectionMultiplexer redis) => _db = redis.GetDatabase();

    // ── Key builders ──────────────────────────────────────────────────────────
    private static string ConnKey(Guid deviceId) => $"signaling:conn:{deviceId}";
    private static string DeviceKey(string connectionId) => $"signaling:device:{connectionId}";
    private static string UserSessionsKey(Guid userId) => $"signaling:user-sessions:{userId}";
    private static string SessionInfoKey(Guid deviceId) => $"signaling:session:{deviceId}";
    private static string FolderOwnersKey(Guid folderId) => $"signaling:folder-owners:{folderId}";
    private static string PairKey(string connId) => $"signaling:pair:{connId}";

    // ── Device ↔ Connection ───────────────────────────────────────────────────

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

    public async Task RemoveDeviceConnectionAsync(Guid deviceId, string connectionId, Guid userId, CancellationToken ct = default)
    {
        var current = (string?)await _db.StringGetAsync(ConnKey(deviceId));
        if (current != connectionId) return; // stale call — another connection already registered

        var batch = _db.CreateBatch();
        var t1 = batch.KeyDeleteAsync(ConnKey(deviceId));
        var t2 = batch.KeyDeleteAsync(DeviceKey(connectionId));
        var t3 = batch.KeyDeleteAsync(SessionInfoKey(deviceId));
        var t4 = batch.SetRemoveAsync(UserSessionsKey(userId), deviceId.ToString());
        batch.Execute();
        await Task.WhenAll(t1, t2, t3, t4);
    }

    public async Task<string?> GetConnectionIdByDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(ConnKey(deviceId));
        return val.HasValue ? (string?)val : null;
    }

    public async Task<Guid?> GetDeviceIdByConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(DeviceKey(connectionId));
        return val.HasValue && Guid.TryParse((string?)val, out var id) ? id : null;
    }

    public Task<bool> IsDeviceOnlineAsync(Guid deviceId, CancellationToken ct = default)
        => _db.KeyExistsAsync(ConnKey(deviceId));

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

    public async Task<SessionInfo?> GetSessionInfoAsync(Guid deviceId, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(SessionInfoKey(deviceId));
        if (!val.HasValue) return null;
        return JsonSerializer.Deserialize<SessionInfo>((string)val!);
    }

    // ── Folder owner tracking ─────────────────────────────────────────────────

    public async Task RegisterOwnerAsync(Guid folderId, Guid deviceId, CancellationToken ct = default)
    {
        await _db.SetAddAsync(FolderOwnersKey(folderId), deviceId.ToString());
        await _db.KeyExpireAsync(FolderOwnersKey(folderId), DeviceTtl);
    }

    public async Task<bool> RemoveOwnerAsync(Guid folderId, Guid deviceId, CancellationToken ct = default)
    {
        await _db.SetRemoveAsync(FolderOwnersKey(folderId), deviceId.ToString());
        var remaining = await _db.SetLengthAsync(FolderOwnersKey(folderId));
        return remaining == 0;
    }

    public async Task<Guid?> GetOwnerDeviceIdAsync(Guid folderId, CancellationToken ct = default)
    {
        var val = await _db.SetRandomMemberAsync(FolderOwnersKey(folderId));
        if (!val.HasValue || !Guid.TryParse((string?)val, out var deviceId)) return null;

        // Lazy cleanup: if the connection is gone, remove stale entry
        var connId = await GetConnectionIdByDeviceAsync(deviceId, ct);
        if (connId is null)
        {
            await RemoveOwnerAsync(folderId, deviceId, ct);
            return null;
        }

        return deviceId;
    }

    // ── P2P session pairs ─────────────────────────────────────────────────────

    public async Task RegisterSessionPairAsync(string receiverConnId, string ownerConnId, Guid folderId, CancellationToken ct = default)
    {
        var batch = _db.CreateBatch();
        var t1 = batch.SetAddAsync(PairKey(receiverConnId), ownerConnId);
        var t2 = batch.KeyExpireAsync(PairKey(receiverConnId), SessionTtl);
        var t3 = batch.SetAddAsync(PairKey(ownerConnId), receiverConnId);
        var t4 = batch.KeyExpireAsync(PairKey(ownerConnId), SessionTtl);
        batch.Execute();
        await Task.WhenAll(t1, t2, t3, t4);
    }

    public Task<bool> IsValidSessionPairAsync(string connA, string connB, CancellationToken ct = default)
        => _db.SetContainsAsync(PairKey(connA), connB);

    public async Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        var peers = await _db.SetMembersAsync(PairKey(connectionId));
        foreach (var peer in peers)
            await _db.SetRemoveAsync(PairKey(peer!), connectionId);
        await _db.KeyDeleteAsync(PairKey(connectionId));
    }
}
