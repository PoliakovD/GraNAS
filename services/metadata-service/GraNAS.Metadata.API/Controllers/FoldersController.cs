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

  public FoldersController(
    IFolderService folderService,
    IPermissionService permissionService,
    IAuthorizationService authorizationService)
  {
    _folderService = folderService;
    _permissionService = permissionService;
    _authorizationService = authorizationService;
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

    return result.Error switch
    {
      CreateFolderError.None => CreatedAtAction(nameof(GetFolders), new { id = result.Response!.Id }, result.Response),
      CreateFolderError.ParentNotFoundOrForbidden => NotFound(new ErrorResponse
      {
        Error = "parent_folder_not_found",
        ErrorDescription = "Parent folder not found or access denied."
      }),
      _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
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

    return result.Error switch
    {
      DeleteFolderError.None => NoContent(),
      DeleteFolderError.NotFound => NotFound(new ErrorResponse
      {
        Error = "folder_not_found",
        ErrorDescription = "Folder not found."
      }),
      DeleteFolderError.Forbidden => Forbid(),
      _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
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

    return result.Error switch
    {
      GrantPermissionError.None => StatusCode(StatusCodes.Status201Created, result.Response),
      GrantPermissionError.FolderNotFoundOrForbidden => NotFound(new ErrorResponse
      {
        Error = "folder_not_found",
        ErrorDescription = "Folder not found or access denied."
      }),
      GrantPermissionError.UserNotFound => NotFound(new ErrorResponse
      {
        Error = "user_not_found",
        ErrorDescription = "User with the specified email not found."
      }),
      _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
  }

  /// <summary>Отозвать права пользователя на папку</summary>
  [HttpDelete("{id}/permissions/{userId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> RevokePermission(Guid id, Guid userId)
  {
    var ownerId = GetCurrentUserId();
    if (ownerId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var result = await _permissionService.RevokeAsync(ownerId.Value, id, userId);

    return result.Error switch
    {
      RevokePermissionError.None => NoContent(),
      RevokePermissionError.FolderNotFoundOrForbidden => NotFound(new ErrorResponse
      {
        Error = "folder_not_found",
        ErrorDescription = "Folder not found or access denied."
      }),
      RevokePermissionError.PermissionNotFound => NotFound(new ErrorResponse
      {
        Error = "permission_not_found",
        ErrorDescription = "Permission not found for the specified user."
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
