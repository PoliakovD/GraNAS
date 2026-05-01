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

[ApiController]
[Route("api/signaling/devices")]
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

    [HttpGet]
    [ProducesResponseType(typeof(List<DeviceResponse>), 200)]
    public async Task<ActionResult<List<DeviceResponse>>> List(CancellationToken ct)
    {
        var userId = GetUserId();
        var devices = await _deviceService.GetByUserAsync(userId, ct);
        _logger.LogDebug("Devices listed for user {UserId}: count={Count}", userId, devices.Count);
        return Ok(devices);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
