using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.API.Controllers;

/// <summary>
/// Контроллер управления устройствами пользователя и device-folder binding.
/// </summary>
[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IDeviceService deviceService, ILogger<DevicesController> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    /// <summary>
    /// Регистрирует устройство или обновляет его имя и платформу.
    /// Возвращает 409, если устройство с таким <c>DeviceId</c> принадлежит другому пользователю.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DeviceResponse), 200)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<DeviceResponse>> Register(
        [FromBody] DeviceRegistrationRequest req, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var result = await _deviceService.RegisterOrUpdateAsync(userId, req, ct);
            _logger.LogInformation("Device registered: id={DeviceId} for userId={UserId}", result.DeviceId, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("different user"))
        {
            _logger.LogWarning("Device registration conflict: device {DeviceId} is owned by another user", req.DeviceId);
            return Conflict(new { error = "DEVICE_OWNED_BY_OTHER_USER", message = ex.Message });
        }
    }

    /// <summary>Возвращает список всех устройств текущего пользователя с актуальным онлайн-статусом.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<DeviceResponse>), 200)]
    public async Task<ActionResult<List<DeviceResponse>>> List(CancellationToken ct)
    {
        var userId = GetUserId();
        var devices = await _deviceService.GetByUserAsync(userId, ct);
        _logger.LogDebug("Devices listed for user {UserId}: count={Count}", userId, devices.Count);
        return Ok(devices);
    }

    /// <summary>Batch: какой девайс привязан к каждой из указанных папок.</summary>
    [HttpGet("folder-devices")]
    [ProducesResponseType(typeof(List<FolderDeviceResponse>), 200)]
    public async Task<ActionResult<List<FolderDeviceResponse>>> GetFolderDevices(
        [FromQuery] Guid[] folderIds, CancellationToken ct)
    {
        if (folderIds.Length == 0) return Ok(Array.Empty<FolderDeviceResponse>());
        var userId = GetUserId();
        return Ok(await _deviceService.GetFolderDevicesAsync(folderIds, userId, ct));
    }

    /// <summary>Явно привязать папку к этому девайсу.</summary>
    [HttpPost("{deviceId:guid}/folders/{folderId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(FolderDeviceResponse), 409)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ClaimFolder(
        Guid deviceId, Guid folderId,
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (!await _deviceService.BelongsToUserAsync(deviceId, userId, ct))
            return Forbid();

        var conflict = await _deviceService.TryClaimFolderAsync(deviceId, folderId, force, ct);
        if (conflict is not null)
        {
            var resp = new FolderDeviceResponse
            {
                FolderId = conflict.FolderId,
                DeviceId = conflict.DeviceId,
                DeviceName = conflict.Device.DeviceName,
                Platform = conflict.Device.Platform,
                ClaimedAt = conflict.ClaimedAt,
            };
            return Conflict(resp);
        }

        return NoContent();
    }

    /// <summary>Снять привязку папки с этого девайса.</summary>
    [HttpDelete("{deviceId:guid}/folders/{folderId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ReleaseFolder(
        Guid deviceId, Guid folderId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!await _deviceService.BelongsToUserAsync(deviceId, userId, ct))
            return Forbid();

        await _deviceService.ReleaseFolderAsync(deviceId, folderId, ct);
        return NoContent();
    }

    /// <summary>
    /// Переименовывает устройство.
    /// Возвращает 404, если устройство не найдено или не принадлежит текущему пользователю.
    /// Возвращает 409, если имя уже занято другим устройством того же пользователя.
    /// </summary>
    [HttpPatch("{deviceId:guid}")]
    [ProducesResponseType(typeof(DeviceResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<DeviceResponse>> Rename(
        Guid deviceId, [FromBody] DeviceRenameRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        if (!await _deviceService.BelongsToUserAsync(deviceId, userId, ct))
            return Forbid();

        var result = await _deviceService.RenameAsync(deviceId, req.DeviceName, ct);
        if (result is null)
        {
            _logger.LogWarning("Rename conflict: device {DeviceId} name={Name}", deviceId, req.DeviceName);
            return Conflict(new { error = "NAME_CONFLICT", message = "Устройство с таким именем уже существует." });
        }

        return Ok(result);
    }

    /// <summary>Возвращает список папок, привязанных к указанному устройству.</summary>
    [HttpGet("{deviceId:guid}/folders")]
    [ProducesResponseType(typeof(List<DeviceFolderResponse>), 200)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<List<DeviceFolderResponse>>> GetDeviceFolders(
        Guid deviceId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!await _deviceService.BelongsToUserAsync(deviceId, userId, ct))
            return Forbid();

        var folders = await _deviceService.GetFoldersByDeviceAsync(deviceId, ct);
        _logger.LogDebug("Device folders listed: deviceId={DeviceId} count={Count}", deviceId, folders.Count);
        return Ok(folders);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
