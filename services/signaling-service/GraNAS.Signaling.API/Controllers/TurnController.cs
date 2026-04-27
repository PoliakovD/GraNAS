using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Interfaces;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GraNAS.Signaling.API.Controllers;

[Authorize]
[ApiController]
[Route("api/turn")]
[Produces("application/json")]
public class TurnController : ControllerBase
{
    private readonly ITurnCredentialService _turnService;

    public TurnController(ITurnCredentialService turnService) => _turnService = turnService;

    /// <summary>Получить временные TURN-учётки для WebRTC (TTL ≈ 10 мин, RFC 8489)</summary>
    [HttpGet("credentials")]
    [ProducesResponseType(typeof(TurnCredentialsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult GetCredentials()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? "anonymous";

        var creds = _turnService.Generate(userId);
        return Ok(new TurnCredentialsResponse(creds.Username, creds.Credential, creds.Uris, creds.Ttl));
    }
}
