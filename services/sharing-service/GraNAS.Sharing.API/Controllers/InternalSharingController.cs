using System;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Sharing.Models.DTO;
using GraNAS.Sharing.Services.Interfaces;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GraNAS.Sharing.API.Controllers;

[Authorize]
[ApiController]
[Route("api/internal/shares")]
[Produces("application/json")]
public class InternalSharingController : ControllerBase
{
    private readonly IShareService _shareService;
    private readonly ILogger<InternalSharingController> _logger;

    public InternalSharingController(IShareService shareService, ILogger<InternalSharingController> logger)
    {
        _shareService = shareService;
        _logger = logger;
    }

    /// <summary>Получить метаданные share-ссылки по хэшу токена (межсервисный вызов из signaling-service)</summary>
    [HttpGet("by-token-hash/{tokenHash}")]
    [ProducesResponseType(typeof(ShareAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetByTokenHash(string tokenHash, CancellationToken ct)
    {
        var shareLink = await _shareService.GetByTokenHashInternalAsync(tokenHash, ct);
        if (shareLink is null)
        {
            _logger.LogDebug("Internal lookup: share by tokenHash not found");
            return NotFound(new ErrorResponse
            {
                Error = "share_not_found",
                ErrorDescription = $"Share link with token hash '{tokenHash}' not found."
            });
        }

        return Ok(new ShareAccessResponse
        {
            FolderId = shareLink.FolderId,
            OwnerId = shareLink.OwnerId,
            ScopePath = shareLink.Path,
            ExpiresAt = shareLink.ExpiresAt,
            Revoked = shareLink.Revoked
        });
    }
}
