using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Models.Repositories;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.Services.Implementations;

/// <summary>
/// Сервис управления устройствами и device-folder binding.
/// Оркестрирует взаимодействие между репозиториями PostgreSQL и Redis-сессиями
/// для обеспечения актуального онлайн-статуса в ответах API.
/// </summary>
public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _repo;
    private readonly IDeviceFolderRepository _folderRepo;
    private readonly ISessionStore _sessions;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(IDeviceRepository repo, IDeviceFolderRepository folderRepo, ISessionStore sessions, ILogger<DeviceService> logger)
    {
        _repo = repo;
        _folderRepo = folderRepo;
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

    public async Task<DeviceFolder?> TryClaimFolderAsync(Guid deviceId, Guid folderId, bool force, CancellationToken ct = default)
    {
        if (!force)
        {
            var existing = await _folderRepo.GetByFolderIdAsync(folderId, ct);
            if (existing is not null && existing.DeviceId != deviceId)
            {
                _logger.LogDebug("ClaimFolder conflict: folder {FolderId} already on device {ExistingDevice}, requester={DeviceId}",
                    folderId, existing.DeviceId, deviceId);
                return existing; // conflict
            }
        }

        await _folderRepo.ClaimAsync(deviceId, folderId, ct);
        _logger.LogInformation("Folder {FolderId} claimed by device {DeviceId} (force={Force})", folderId, deviceId, force);
        return null; // success
    }

    public async Task ReleaseFolderAsync(Guid deviceId, Guid folderId, CancellationToken ct = default)
    {
        await _folderRepo.ReleaseAsync(deviceId, folderId, ct);
        _logger.LogInformation("Folder {FolderId} released from device {DeviceId}", folderId, deviceId);
    }

    public async Task<List<FolderDeviceResponse>> GetFolderDevicesAsync(IEnumerable<Guid> folderIds, Guid userId, CancellationToken ct = default)
    {
        var bindings = await _folderRepo.GetByFolderIdsAsync(folderIds, userId, ct);
        var result = new List<FolderDeviceResponse>(bindings.Count);

        foreach (var b in bindings)
        {
            var isOnline = await _sessions.IsDeviceOnlineAsync(b.DeviceId, ct);
            result.Add(new FolderDeviceResponse
            {
                FolderId = b.FolderId,
                DeviceId = b.DeviceId,
                DeviceName = b.Device.DeviceName,
                Platform = b.Device.Platform,
                IsOnline = isOnline,
                ClaimedAt = b.ClaimedAt,
            });
        }

        return result;
    }

    public async Task<Guid?> GetBoundDeviceIdAsync(Guid folderId, CancellationToken ct = default)
    {
        var binding = await _folderRepo.GetByFolderIdAsync(folderId, ct);
        return binding?.DeviceId;
    }

    public async Task<DeviceResponse?> RenameAsync(Guid deviceId, string newName, CancellationToken ct = default)
    {
        var ok = await _repo.RenameAsync(deviceId, newName, ct);
        if (!ok)
        {
            _logger.LogDebug("Rename conflict: device {DeviceId} name={Name}", deviceId, newName);
            return null;
        }

        var device = await _repo.GetByIdAsync(deviceId, ct);
        if (device is null) return null;
        var isOnline = await _sessions.IsDeviceOnlineAsync(deviceId, ct);
        _logger.LogInformation("Device renamed: id={DeviceId} newName={Name}", deviceId, newName);
        return MapToResponse(device, isOnline);
    }

    public async Task<List<DeviceFolderResponse>> GetFoldersByDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        var bindings = await _folderRepo.GetByDeviceIdAsync(deviceId, ct);
        return bindings.Select(b => new DeviceFolderResponse
        {
            FolderId = b.FolderId,
            ClaimedAt = b.ClaimedAt,
        }).ToList();
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
