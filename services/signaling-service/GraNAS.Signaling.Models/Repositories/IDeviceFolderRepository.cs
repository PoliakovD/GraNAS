namespace GraNAS.Signaling.Models.Repositories;

public interface IDeviceFolderRepository
{
    Task<DeviceFolder?> GetByFolderIdAsync(Guid folderId, CancellationToken ct = default);
    Task ClaimAsync(Guid deviceId, Guid folderId, CancellationToken ct = default);
    Task ReleaseAsync(Guid deviceId, Guid folderId, CancellationToken ct = default);
    Task<List<DeviceFolder>> GetByFolderIdsAsync(IEnumerable<Guid> folderIds, Guid userId, CancellationToken ct = default);
    Task RemoveAllByDeviceAsync(Guid deviceId, CancellationToken ct = default);
}
