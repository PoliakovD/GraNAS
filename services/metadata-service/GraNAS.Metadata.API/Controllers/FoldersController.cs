using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Services.Interfaces;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace GraNAS.Metadata.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[EnableRateLimiting("api")]
public class FoldersController : ControllerBase
{
  private readonly IFolderService _folderService;
  private readonly IPermissionService _permissionService;
  private readonly IAuthorizationService _authorizationService;
  private readonly ILogger<FoldersController> _logger;

  public FoldersController(
    IFolderService folderService,
    IPermissionService permissionService,
    IAuthorizationService authorizationService,
    ILogger<FoldersController> logger)
  {
    _folderService = folderService;
    _permissionService = permissionService;
    _authorizationService = authorizationService;
    _logger = logger;
  }

  /// <summary>Получить список папок текущего пользователя (свои + расшаренные)</summary>
  [HttpGet]
  [ProducesResponseType(typeof(FolderResponse[]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> GetFolders()
  {
    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var folders = await _folderService.GetUserFoldersAsync(userId.Value);
    return Ok(folders);
  }

  /// <summary>Создать новую папку</summary>
  [HttpPost]
  [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
  {
    if (!ModelState.IsValid)
      return BadRequest(ModelState);

    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var result = await _folderService.CreateFolderAsync(userId.Value, request);

    if (result.Error == CreateFolderError.ParentNotFoundOrForbidden)
    {
      _logger.LogWarning("Folder create rejected: parent {ParentFolderId} not found or not owned (userId={UserId})",
        request.ParentFolderId, userId);
      return NotFound(new ErrorResponse
      {
        Error = "parent_folder_not_found",
        ErrorDescription = "Parent folder not found or access denied."
      });
    }

    if (result.Error != CreateFolderError.None)
      return StatusCode(StatusCodes.Status500InternalServerError);

    _logger.LogInformation("Folder created: id={FolderId} parent={ParentFolderId} userId={UserId}",
      result.Response!.Id, request.ParentFolderId, userId);
    return CreatedAtAction(nameof(GetFolders), new { id = result.Response.Id }, result.Response);
  }

  /// <summary>Удалить папку (только владелец)</summary>
  [HttpDelete("{id}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> DeleteFolder(Guid id)
  {
    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var result = await _folderService.DeleteFolderAsync(userId.Value, id);

    if (result.Error == DeleteFolderError.NotFound)
    {
      _logger.LogWarning("Folder delete rejected: {FolderId} not found (userId={UserId})", id, userId);
      return NotFound(new ErrorResponse
      {
        Error = "folder_not_found",
        ErrorDescription = "Folder not found."
      });
    }

    if (result.Error == DeleteFolderError.Forbidden)
    {
      _logger.LogWarning("Folder delete rejected: {FolderId} not owned by userId={UserId}", id, userId);
      return Forbid();
    }

    if (result.Error != DeleteFolderError.None)
      return StatusCode(StatusCodes.Status500InternalServerError);

    _logger.LogInformation("Folder deleted: id={FolderId} userId={UserId}", id, userId);
    return NoContent();
  }

  /// <summary>Получить список прав на папку (только владелец)</summary>
  [HttpGet("{id}/permissions")]
  [ProducesResponseType(typeof(PermissionResponse[]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> GetFolderPermissions(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var result = await _permissionService.ListByFolderAsync(userId.Value, id, ct);

    if (result is null)
    {
      _logger.LogWarning("List permissions denied: not owner of folder {FolderId} (userId={UserId})", id, userId);
      return NotFound(new ErrorResponse
      {
        Error = "folder_not_found",
        ErrorDescription = "Folder not found or access denied."
      });
    }

    return Ok(result);
  }

  /// <summary>Выдать права на папку другому пользователю</summary>
  [HttpPost("{id}/permissions")]
  [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> GrantPermission(
    Guid id,
    [FromBody] GrantPermissionRequest request,
    CancellationToken ct)
  {
    if (!ModelState.IsValid)
      return BadRequest(ModelState);

    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var result = await _permissionService.GrantAsync(userId.Value, id, request, ct);

    if (result.Error == GrantPermissionError.FolderNotFoundOrForbidden)
    {
      _logger.LogWarning("Permission grant rejected: folder {FolderId} not found or not owned (userId={UserId})", id, userId);
      return NotFound(new ErrorResponse
      {
        Error = "folder_not_found",
        ErrorDescription = "Folder not found or access denied."
      });
    }

    if (result.Error == GrantPermissionError.UserNotFound)
    {
      _logger.LogWarning("Permission grant rejected: target user {Email} not found (folder={FolderId})", request.Email, id);
      return NotFound(new ErrorResponse
      {
        Error = "user_not_found",
        ErrorDescription = "User with the specified email not found."
      });
    }

    if (result.Error != GrantPermissionError.None)
      return StatusCode(StatusCodes.Status500InternalServerError);

    _logger.LogInformation("Permission granted on folder {FolderId} to {Email} ({AccessLevel}) by userId={UserId}",
      id, request.Email, request.AccessLevel, userId);
    return StatusCode(StatusCodes.Status201Created, result.Response);
  }

  /// <summary>Отозвать права пользователя на папку</summary>
  [HttpDelete("{id}/permissions/{userId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> RevokePermission(Guid id, Guid userId)
  {
    var ownerId = GetCurrentUserId();
    if (ownerId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var result = await _permissionService.RevokeAsync(ownerId.Value, id, userId);

    if (result.Error == RevokePermissionError.FolderNotFoundOrForbidden)
    {
      _logger.LogWarning("Permission revoke rejected: folder {FolderId} not found or not owned (ownerId={OwnerId})", id, ownerId);
      return NotFound(new ErrorResponse
      {
        Error = "folder_not_found",
        ErrorDescription = "Folder not found or access denied."
      });
    }

    if (result.Error == RevokePermissionError.PermissionNotFound)
    {
      _logger.LogWarning("Permission revoke rejected: no permission for user {TargetUserId} on folder {FolderId}", userId, id);
      return NotFound(new ErrorResponse
      {
        Error = "permission_not_found",
        ErrorDescription = "Permission not found for the specified user."
      });
    }

    if (result.Error != RevokePermissionError.None)
      return StatusCode(StatusCodes.Status500InternalServerError);

    _logger.LogInformation("Permission revoked on folder {FolderId} from user {TargetUserId} by ownerId={OwnerId}",
      id, userId, ownerId);
    return Ok("User`s access revoked");
  }

  /// <summary>Обновить last_accessed_at папки (вызывается при открытии)</summary>
  [HttpPatch("{folderId:guid}/touch")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Touch(Guid folderId, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var ok = await _folderService.TouchAsync(folderId, userId.Value, ct);
    return ok ? NoContent() : NotFound(new ErrorResponse { Error = "folder_not_found", ErrorDescription = "Folder not found or access denied." });
  }

  private Guid? GetCurrentUserId()
  {
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
  }
}
