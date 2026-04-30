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

namespace GraNAS.Signaling.API.Controllers;

[ApiController]
[Route("api/signaling/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionStore _sessions;
    private readonly IDeviceRepository _deviceRepo;
    private readonly IHubContext<SignalingHub> _hub;

    public SessionsController(ISessionStore sessions, IDeviceRepository deviceRepo, IHubContext<SignalingHub> hub)
    {
        _sessions = sessions;
        _deviceRepo = deviceRepo;
        _hub = hub;
    }

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

        return Ok(responses);
    }

    [HttpDelete("{deviceId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> Terminate(Guid deviceId, CancellationToken ct)
    {
        var userId = GetUserId();

        if (!await _deviceRepo.BelongsToUserAsync(deviceId, userId, ct))
            return NotFound();

        var info = await _sessions.GetSessionInfoAsync(deviceId, ct);
        if (info is not null)
        {
            await _hub.Clients.Client(info.ConnectionId).SendAsync("ForceDisconnect", ct);
            await _sessions.RemoveDeviceConnectionAsync(deviceId, info.ConnectionId, userId, ct);
        }

        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
