using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Services.Interfaces;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GraNAS.Metadata.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FoldersController : ControllerBase
{
  private readonly IFolderService _folderService;

  public FoldersController(IFolderService folderService)
  {
    _folderService = folderService;
  }

  /// <summary>Получить список всех папок текущего пользователя</summary>
  [HttpGet]
  [ProducesResponseType(typeof(FolderResponse[]), StatusCodes.Status200OK)]
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
  public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
  {
    if (!ModelState.IsValid)
      return BadRequest(ModelState);

    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var response = await _folderService.CreateFolderAsync(userId.Value, request);
    return CreatedAtAction(nameof(GetFolders), new { id = response.Id }, response);
  }

  /// <summary>Удалить папку</summary>
  [HttpDelete("{id}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
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
      DeleteFolderError.NotEmpty => Conflict(new ErrorResponse
      {
        Error = "folder_not_empty",
        ErrorDescription = $"Folder contains {result.FilesCount} file(s). Delete files first or confirm deletion."
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
