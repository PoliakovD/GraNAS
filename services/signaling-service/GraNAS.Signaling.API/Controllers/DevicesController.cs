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

namespace GraNAS.Signaling.API.Controllers;

[ApiController]
[Route("api/signaling/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService) => _deviceService = deviceService;

    [HttpPost]
    [ProducesResponseType(typeof(DeviceResponse), 200)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<DeviceResponse>> Register(
        [FromBody] DeviceRegistrationRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _deviceService.RegisterOrUpdateAsync(GetUserId(), req, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("different user"))
        {
            return Conflict(new { error = "DEVICE_OWNED_BY_OTHER_USER", message = ex.Message });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<DeviceResponse>), 200)]
    public async Task<ActionResult<List<DeviceResponse>>> List(CancellationToken ct)
        => Ok(await _deviceService.GetByUserAsync(GetUserId(), ct));

    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
