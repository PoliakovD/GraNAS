using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Interfaces;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.API.Controllers;

/// <summary>
/// Контроллер выдачи краткосрочных TURN-учётных данных для WebRTC-соединений.
/// Только для аутентифицированных пользователей.
/// </summary>
[Authorize]
[ApiController]
[Route("api/turn")]
[Produces("application/json")]
public class TurnController : ControllerBase
{
    private readonly ITurnCredentialService _turnService;
    private readonly ILogger<TurnController> _logger;

    public TurnController(ITurnCredentialService turnService, ILogger<TurnController> logger)
    {
        _turnService = turnService;
        _logger = logger;
    }

    /// <summary>Возвращает краткосрочные TURN-учётные данные для текущего пользователя (RFC 8489, TTL ≈ 10 мин).</summary>
    /// <remarks>
    /// UserId извлекается из JWT-клейма (<c>NameIdentifier</c> или <c>sub</c>).
    /// Учётные данные передаются клиенту для конфигурации <c>RTCPeerConnection.iceServers</c>.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("credentials")]
    [ProducesResponseType(typeof(TurnCredentialsResponse), StatusCodes.Status200OK)]
    public IActionResult GetCredentials()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? "anonymous";

        var creds = _turnService.Generate(userId);
        _logger.LogDebug("TURN credentials issued for userId={UserId}", userId);
        return Ok(new TurnCredentialsResponse(creds.Username, creds.Credential, creds.Uris, creds.Ttl));
    }
}
