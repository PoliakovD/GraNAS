using System;
using System.Threading.Tasks;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Models.Repositories;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GraNAS.Metadata.API.Controllers;

[Authorize]
[ApiController]
[Route("api/internal/folders")]
[Produces("application/json")]
public class InternalFoldersController : ControllerBase
{
    private readonly IFolderRepository _folders;
    private readonly IPermissionRepository _permissions;
    private readonly ILogger<InternalFoldersController> _logger;

    public InternalFoldersController(
        IFolderRepository folders,
        IPermissionRepository permissions,
        ILogger<InternalFoldersController> logger)
    {
        _folders = folders;
        _permissions = permissions;
        _logger = logger;
    }

    /// <summary>Получить метаданные папки (межсервисный вызов)</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FolderLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var folder = await _folders.GetByIdAsync(id);
        if (folder is null)
        {
            _logger.LogDebug("Internal lookup: folder {FolderId} not found", id);
            return NotFound(new ErrorResponse
            {
                Error = "folder_not_found",
                ErrorDescription = $"Folder '{id}' not found."
            });
        }

        return Ok(new FolderLookupResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            OwnerId = folder.OwnerId
        });
    }

    /// <summary>Проверить доступ пользователя к папке (для signaling-service)</summary>
    [HttpGet("{id:guid}/access")]
    [ProducesResponseType(typeof(FolderAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAccess(Guid id, [FromQuery] Guid userId)
    {
        var folder = await _folders.GetByIdAsync(id);
        if (folder is null)
        {
            _logger.LogDebug("Internal lookup: folder {FolderId} not found (access check for userId={UserId})", id, userId);
            return NotFound(new ErrorResponse { Error = "folder_not_found", ErrorDescription = $"Folder '{id}' not found." });
        }

        if (folder.OwnerId == userId)
            return Ok(new FolderAccessResponse { FolderId = folder.Id, OwnerId = folder.OwnerId, ScopePath = null });

        var permission = await _permissions.GetAsync(id, userId);
        if (permission is not null)
            return Ok(new FolderAccessResponse { FolderId = folder.Id, OwnerId = folder.OwnerId, ScopePath = permission.Path });

        _logger.LogDebug("Internal lookup: user {QueriedUserId} has no access to folder {FolderId}", userId, id);
        return NotFound(new ErrorResponse { Error = "access_denied", ErrorDescription = $"User '{userId}' has no access to folder '{id}'." });
    }

    /// <summary>Проверить владение папкой (межсервисный вызов из sharing-service)</summary>
    [HttpGet("{id:guid}/owner/{ownerId:guid}")]
    [ProducesResponseType(typeof(FolderLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetByIdForOwner(Guid id, Guid ownerId)
    {
        var folder = await _folders.GetByIdForOwnerAsync(id, ownerId);
        if (folder is null)
        {
            _logger.LogDebug("Internal lookup: folder {FolderId} not owned by {OwnerId}", id, ownerId);
            return NotFound(new ErrorResponse
            {
                Error = "folder_not_found",
                ErrorDescription = $"Folder '{id}' not found or does not belong to owner '{ownerId}'."
            });
        }

        return Ok(new FolderLookupResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            OwnerId = folder.OwnerId
        });
    }
}
