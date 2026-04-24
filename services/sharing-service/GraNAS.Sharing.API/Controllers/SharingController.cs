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

namespace GraNAS.Sharing.API.Controllers;

[Authorize]
[ApiController]
[Produces("application/json")]
[EnableRateLimiting("api")]
public class SharingController : ControllerBase
{
    private readonly IShareService _shareService;

    public SharingController(IShareService shareService)
    {
        _shareService = shareService;
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

        return result.Error switch
        {
            CreateShareError.None => StatusCode(StatusCodes.Status201Created, result.Response),
            CreateShareError.FolderNotFoundOrForbidden => NotFound(new ErrorResponse
            {
                Error = "folder_not_found",
                ErrorDescription = "Folder not found or access denied."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
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

        return result.Error switch
        {
            RevokeShareError.None => NoContent(),
            RevokeShareError.NotFoundOrForbidden => NotFound(new ErrorResponse
            {
                Error = "share_link_not_found",
                ErrorDescription = "Share link not found or access denied."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
