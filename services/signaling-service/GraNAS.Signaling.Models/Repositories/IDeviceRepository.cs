namespace GraNAS.Signaling.Models.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Device>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    /// <returns>The upserted device.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the device belongs to a different user.</exception>
    Task<Device> UpsertAsync(Device device, CancellationToken ct = default);
    Task<bool> BelongsToUserAsync(Guid deviceId, Guid userId, CancellationToken ct = default);
    Task TouchLastSeenAsync(Guid id, CancellationToken ct = default);
}
