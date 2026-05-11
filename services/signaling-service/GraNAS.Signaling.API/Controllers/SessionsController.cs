using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Signaling.API.Hubs;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Models.Repositories;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.API.Controllers;

/// <summary>
/// Контроллер управления активными SignalR-сессиями устройств пользователя.
/// Позволяет просматривать онлайн-устройства и принудительно завершать их сессии.
/// </summary>
[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionStore _sessions;
    private readonly IDeviceRepository _deviceRepo;
    private readonly IHubContext<SignalingHub> _hub;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionStore sessions,
        IDeviceRepository deviceRepo,
        IHubContext<SignalingHub> hub,
        ILogger<SessionsController> logger)
    {
        _sessions = sessions;
        _deviceRepo = deviceRepo;
        _hub = hub;
        _logger = logger;
    }

    /// <summary>Возвращает список активных онлайн-сессий всех устройств текущего пользователя.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ActiveSessionResponse>), 200)]
    public async Task<ActionResult<List<ActiveSessionResponse>>> List(CancellationToken ct)
    {
        var userId = GetUserId();
        var deviceIds = await _sessions.GetOnlineDevicesByUserAsync(userId, ct);

        var responses = new List<ActiveSessionResponse>(deviceIds.Count);
        foreach (var deviceId in deviceIds)
        {
            var info = await _sessions.GetSessionInfoAsync(deviceId, ct);
            if (info is null) continue;

            var device = await _deviceRepo.GetByIdAsync(deviceId, ct);
            if (device is null) continue;

            responses.Add(new ActiveSessionResponse
            {
                DeviceId = deviceId,
                DeviceName = device.DeviceName,
                Platform = device.Platform,
                Ip = info.Ip,
                ConnectedAt = info.ConnectedAt
            });
        }

        _logger.LogDebug("Sessions listed for user {UserId}: count={Count}", userId, responses.Count);
        return Ok(responses);
    }

    /// <summary>
    /// Принудительно завершает сессию указанного устройства.
    /// Отправляет устройству событие <c>ForceDisconnect</c> через SignalR и очищает Redis-состояние.
    /// Возвращает 404, если устройство не принадлежит текущему пользователю.
    /// </summary>
    [HttpDelete("{deviceId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> Terminate(Guid deviceId, CancellationToken ct)
    {
        var userId = GetUserId();

        if (!await _deviceRepo.BelongsToUserAsync(deviceId, userId, ct))
        {
            _logger.LogWarning("Terminate session rejected: device {DeviceId} not owned by userId={UserId}", deviceId, userId);
            return NotFound();
        }

        var info = await _sessions.GetSessionInfoAsync(deviceId, ct);
        if (info is not null)
        {
            await _hub.Clients.Client(info.ConnectionId).SendAsync("ForceDisconnect", ct);
            await _sessions.RemoveDeviceConnectionAsync(deviceId, info.ConnectionId, userId, ct);
        }

        _logger.LogInformation("Session terminated: device {DeviceId} (userId={UserId})", deviceId, userId);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
