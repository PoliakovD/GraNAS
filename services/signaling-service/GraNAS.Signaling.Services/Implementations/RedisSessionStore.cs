using GraNAS.Signaling.Services.Interfaces;
using StackExchange.Redis;

namespace GraNAS.Signaling.Services.Implementations;

public class RedisSessionStore : ISessionStore
{
    private readonly IDatabase _db;
    private static readonly TimeSpan OwnerTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(1);

    public RedisSessionStore(IConnectionMultiplexer redis) => _db = redis.GetDatabase();

    private static string OwnerKey(Guid folderId) => $"signaling:owner:{folderId}";
    private static string PairKey(string connId) => $"signaling:pair:{connId}";

    public Task RegisterOwnerAsync(Guid folderId, string connectionId, CancellationToken ct = default)
        => _db.StringSetAsync(OwnerKey(folderId), connectionId, OwnerTtl);

    public async Task RemoveOwnerAsync(Guid folderId, string connectionId, CancellationToken ct = default)
    {
        var current = await _db.StringGetAsync(OwnerKey(folderId));
        if (current == connectionId)
            await _db.KeyDeleteAsync(OwnerKey(folderId));
    }

    public async Task<string?> GetOwnerConnectionIdAsync(Guid folderId, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(OwnerKey(folderId));
        return val.HasValue ? (string?)val : null;
    }

    public async Task RegisterSessionPairAsync(string receiverConnId, string ownerConnId, Guid folderId, CancellationToken ct = default)
    {
        // Both sides can relay to each other; owner can have multiple receivers
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
