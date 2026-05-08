using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.DTO;

namespace GraNAS.Signaling.Services.Interfaces;

public interface IDeviceService
{
    Task<DeviceResponse> RegisterOrUpdateAsync(Guid userId, DeviceRegistrationRequest req, CancellationToken ct = default);
    Task<List<DeviceResponse>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> BelongsToUserAsync(Guid deviceId, Guid userId, CancellationToken ct = default);

    // folder → device binding
    /// <returns>null = success; existing DeviceFolder = conflict (folder claimed by another device of the same user)</returns>
    Task<DeviceFolder?> TryClaimFolderAsync(Guid deviceId, Guid folderId, bool force, CancellationToken ct = default);
    Task ReleaseFolderAsync(Guid deviceId, Guid folderId, CancellationToken ct = default);
    Task<List<FolderDeviceResponse>> GetFolderDevicesAsync(IEnumerable<Guid> folderIds, Guid userId, CancellationToken ct = default);
}
