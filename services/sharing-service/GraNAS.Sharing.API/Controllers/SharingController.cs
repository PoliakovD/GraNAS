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

[Authorize]
[ApiController]
[Produces("application/json")]
[EnableRateLimiting("api")]
public class SharingController : ControllerBase
{
    private readonly IShareService _shareService;
    private readonly ILogger<SharingController> _logger;

    public SharingController(IShareService shareService, ILogger<SharingController> logger)
    {
        _shareService = shareService;
        _logger = logger;
    }

    /// <summary>Создать share-ссылку на папку (только владелец)</summary>
    [HttpPost("api/folders/{folderId:guid}/share")]
    [ProducesResponseType(typeof(CreateShareResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateShare(Guid folderId, [FromBody] CreateShareRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ownerId = GetCurrentUserId();
        if (ownerId is null)
            return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

        var result = await _shareService.CreateAsync(ownerId.Value, folderId, request, ct);

        if (result.Error == CreateShareError.FolderNotFoundOrForbidden)
        {
            _logger.LogWarning("Share link create rejected: folder {FolderId} not found or not owned (ownerId={OwnerId})",
                folderId, ownerId);
            return NotFound(new ErrorResponse
            {
                Error = "folder_not_found",
                ErrorDescription = "Folder not found or access denied."
            });
        }

        if (result.Error != CreateShareError.None)
            return StatusCode(StatusCodes.Status500InternalServerError);

        _logger.LogInformation("Share link created: id={ShareLinkId} folder={FolderId} expiresAt={ExpiresAt} ownerId={OwnerId}",
            result.Response!.Id, folderId, result.Response.ExpiresAt, ownerId);
        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    /// <summary>Список share-ссылок для папки (только владелец, без исходных токенов)</summary>
    [HttpGet("api/folders/{folderId:guid}/shares")]
    [ProducesResponseType(typeof(ShareLinkResponse[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ListShares(Guid folderId)
    {
        var ownerId = GetCurrentUserId();
        if (ownerId is null)
            return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

        var links = await _shareService.ListByFolderAsync(ownerId.Value, folderId);
        return Ok(links);
    }

    /// <summary>Отозвать share-ссылку по внутреннему ID (когда исходный токен утерян)</summary>
    [HttpDelete("api/share-links/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RevokeById(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (ownerId is null)
            return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

        var result = await _shareService.RevokeByIdAsync(ownerId.Value, id);

        if (result.Error == RevokeShareError.NotFoundOrForbidden)
        {
            _logger.LogWarning("Revoke by id rejected: share link {ShareLinkId} not found or not owned (ownerId={OwnerId})",
                id, ownerId);
            return NotFound(new ErrorResponse
            {
                Error = "share_link_not_found",
                ErrorDescription = "Share link not found or access denied."
            });
        }

        if (result.Error != RevokeShareError.None)
            return StatusCode(StatusCodes.Status500InternalServerError);

        _logger.LogInformation("Share link revoked by id: {ShareLinkId} (ownerId={OwnerId})", id, ownerId);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
