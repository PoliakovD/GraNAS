using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Models.Enums;
using GraNAS.Signaling.Models.Repositories;
using GraNAS.Signaling.Services.Interfaces;

namespace GraNAS.Signaling.Services.Implementations;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _repo;
    private readonly ISessionStore _sessions;

    public DeviceService(IDeviceRepository repo, ISessionStore sessions)
    {
        _repo = repo;
        _sessions = sessions;
    }

    public async Task<DeviceResponse> RegisterOrUpdateAsync(Guid userId, DeviceRegistrationRequest req, CancellationToken ct = default)
    {
        var device = new Device
        {
            Id = req.DeviceId,
            UserId = userId,
            DeviceName = req.DeviceName,
            Platform = req.Platform
        };

        var result = await _repo.UpsertAsync(device, ct);
        var isOnline = await _sessions.IsDeviceOnlineAsync(result.Id, ct);

        return MapToResponse(result, isOnline);
    }

    public async Task<List<DeviceResponse>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var devices = await _repo.GetByUserAsync(userId, ct);
        var responses = new List<DeviceResponse>(devices.Count);

        foreach (var d in devices)
        {
            var isOnline = await _sessions.IsDeviceOnlineAsync(d.Id, ct);
            responses.Add(MapToResponse(d, isOnline));
        }

        return responses;
    }

    public Task<bool> BelongsToUserAsync(Guid deviceId, Guid userId, CancellationToken ct = default)
        => _repo.BelongsToUserAsync(deviceId, userId, ct);

    private static DeviceResponse MapToResponse(Device d, bool isOnline) => new()
    {
        DeviceId = d.Id,
        DeviceName = d.DeviceName,
        Platform = d.Platform,
        CreatedAt = d.CreatedAt,
        LastSeenAt = d.LastSeenAt,
        IsOnline = isOnline
    };
}
