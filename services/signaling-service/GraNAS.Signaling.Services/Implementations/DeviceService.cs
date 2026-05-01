using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Models.Repositories;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.Services.Implementations;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _repo;
    private readonly ISessionStore _sessions;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(IDeviceRepository repo, ISessionStore sessions, ILogger<DeviceService> logger)
    {
        _repo = repo;
        _sessions = sessions;
        _logger = logger;
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

        if (result.CreatedAt == result.LastSeenAt || result.LastSeenAt == default)
            _logger.LogInformation("Device registered: id={DeviceId} user={UserId} platform={Platform}",
                result.Id, userId, req.Platform);
        else
            _logger.LogDebug("Device updated: id={DeviceId} user={UserId}", result.Id, userId);

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

    public async Task<bool> BelongsToUserAsync(Guid deviceId, Guid userId, CancellationToken ct = default)
    {
        var belongs = await _repo.BelongsToUserAsync(deviceId, userId, ct);
        if (!belongs)
            _logger.LogDebug("BelongsToUser: device {DeviceId} not owned by {UserId}", deviceId, userId);
        return belongs;
    }

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
