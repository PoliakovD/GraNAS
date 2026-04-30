using GraNAS.Signaling.Models.DTO;

namespace GraNAS.Signaling.Services.Interfaces;

public interface ISessionStore
{
    // ── Device ↔ Connection mapping ─────────────────────────────────────────
    Task RegisterDeviceConnectionAsync(Guid deviceId, string connectionId, Guid userId, string ip, CancellationToken ct = default);
    Task RemoveDeviceConnectionAsync(Guid deviceId, string connectionId, Guid userId, CancellationToken ct = default);
    Task<string?> GetConnectionIdByDeviceAsync(Guid deviceId, CancellationToken ct = default);
    Task<Guid?> GetDeviceIdByConnectionAsync(string connectionId, CancellationToken ct = default);
    Task<bool> IsDeviceOnlineAsync(Guid deviceId, CancellationToken ct = default);
    Task<List<Guid>> GetOnlineDevicesByUserAsync(Guid userId, CancellationToken ct = default);
    Task<SessionInfo?> GetSessionInfoAsync(Guid deviceId, CancellationToken ct = default);

    // ── Folder owner tracking (by deviceId) ─────────────────────────────────
    Task RegisterOwnerAsync(Guid folderId, Guid deviceId, CancellationToken ct = default);
    /// <returns>true if no owners remain online for the folder.</returns>
    Task<bool> RemoveOwnerAsync(Guid folderId, Guid deviceId, CancellationToken ct = default);
    Task<Guid?> GetOwnerDeviceIdAsync(Guid folderId, CancellationToken ct = default);

    // ── P2P session pairs ────────────────────────────────────────────────────
    Task RegisterSessionPairAsync(string receiverConnId, string ownerConnId, Guid folderId, CancellationToken ct = default);
    Task<bool> IsValidSessionPairAsync(string connA, string connB, CancellationToken ct = default);
    Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default);
}
