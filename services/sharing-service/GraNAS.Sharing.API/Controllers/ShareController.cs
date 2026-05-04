using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Sharing.Models.DTO;
using GraNAS.Sharing.Services.Interfaces;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace GraNAS.Sharing.API.Controllers;

[ApiController]
[Route("api/share")]
[Produces("application/json")]
public class ShareController : ControllerBase
{
    private readonly IShareService _shareService;
    private readonly ILogger<ShareController> _logger;

    public ShareController(IShareService shareService, ILogger<ShareController> logger)
    {
        _shareService = shareService;
        _logger = logger;
    }

    /// <summary>Получить метаданные папки по share-токену (публичный эндпоинт)</summary>
    [AllowAnonymous]
    [HttpGet("{token}")]
    [EnableRateLimiting("public")]
    [ProducesResponseType(typeof(ShareDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetByToken(string token, CancellationToken ct)
    {
        var isRevoked = await _shareService.IsRevokedAsync(token);
        if (isRevoked)
        {
            _logger.LogWarning("Share resolve: token revoked");
            return StatusCode(StatusCodes.Status410Gone, new ErrorResponse
            {
                Error = "share_revoked",
                ErrorDescription = "This share link has been revoked."
            });
        }

        var details = await _shareService.GetByTokenAsync(token, ct);
        if (details is null)
        {
            _logger.LogDebug("Share resolve: token not found or expired");
            return NotFound(new ErrorResponse
            {
                Error = "share_not_found",
                ErrorDescription = "Share link not found or has expired."
            });
        }

        _logger.LogDebug("Share resolve: hit (folder={FolderId})", details.FolderId);
        return Ok(details);
    }

    /// <summary>Отозвать share-ссылку по токену (владелец, если сохранил токен)</summary>
    [Authorize]
    [HttpDelete("{token}")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RevokeByToken(string token)
    {
        var ownerId = GetCurrentUserId();
        if (ownerId is null)
            return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

        var result = await _shareService.RevokeByTokenAsync(ownerId.Value, token);

        if (result.Error == RevokeShareError.NotFoundOrForbidden)
        {
            _logger.LogWarning("Revoke by token rejected: not found or not owned (ownerId={OwnerId})", ownerId);
            return NotFound(new ErrorResponse
            {
                Error = "share_link_not_found",
                ErrorDescription = "Share link not found or access denied."
            });
        }

        if (result.Error != RevokeShareError.None)
            return StatusCode(StatusCodes.Status500InternalServerError);

        _logger.LogInformation("Share link revoked by token (ownerId={OwnerId})", ownerId);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
