using GraNAS.Signaling.Models.DTO;

namespace GraNAS.Signaling.Services.Interfaces;

public interface IDeviceService
{
    Task<DeviceResponse> RegisterOrUpdateAsync(Guid userId, DeviceRegistrationRequest req, CancellationToken ct = default);
    Task<List<DeviceResponse>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> BelongsToUserAsync(Guid deviceId, Guid userId, CancellationToken ct = default);
}
