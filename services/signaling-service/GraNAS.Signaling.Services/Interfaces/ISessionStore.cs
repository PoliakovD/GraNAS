namespace GraNAS.Signaling.Services.Interfaces;

public interface ISessionStore
{
    Task RegisterOwnerAsync(Guid folderId, string connectionId, CancellationToken ct = default);
    Task RemoveOwnerAsync(Guid folderId, string connectionId, CancellationToken ct = default);
    Task<string?> GetOwnerConnectionIdAsync(Guid folderId, CancellationToken ct = default);
    Task RegisterSessionPairAsync(string receiverConnId, string ownerConnId, Guid folderId, CancellationToken ct = default);
    Task<bool> IsValidSessionPairAsync(string connA, string connB, CancellationToken ct = default);
    Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default);
}
