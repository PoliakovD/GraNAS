using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Models;
using GraNAS.Models.DTO;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using GraNAS.WebAPI.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GraNAS.WebAPI.Authorization.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FoldersController : ControllerBase
{
  private readonly IFolderRepository _folderRepository;

  public FoldersController(IFolderRepository folderRepository)
  {
    _folderRepository = folderRepository;
  }

  /// <summary>
  /// Получить список всех папок текущего пользователя
  /// </summary>
  /// <returns>Список папок</returns>
  /// <response code="200">Успешно</response>
  /// <response code="401">Не авторизован</response>
  [HttpGet]
  [ProducesResponseType(typeof(FolderResponse[]), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetFolders()
  {

    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var folders = await _folderRepository.GetUserFoldersAsync(userId.Value);

    var response = folders.Select(f => new FolderResponse
    {
      Id = f.Id,
      Name = f.Name,
      CreatedAt = f.CreatedAt,
      UpdatedAt = f.UpdatedAt,
      FilesCount = 0 // можно заполнить отдельным запросом, если необходимо
    }).ToArray();

    return Ok(response);
  }

  /// <summary>
  /// Создать новую папку
  /// </summary>
  /// <param name="request">Название папки</param>
  /// <returns>Созданная папка</returns>
  /// <response code="201">Папка создана</response>
  /// <response code="400">Невалидные данные</response>
  /// <response code="401">Не авторизован</response>
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

    var folder = new Folder
    {
      Id = Guid.NewGuid(),
      OwnerId = userId.Value,
      Name = request.Name,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = null
    };

    await _folderRepository.CreateAsync(folder);

    var response = new FolderResponse
    {
      Id = folder.Id,
      Name = folder.Name,
      CreatedAt = folder.CreatedAt,
      UpdatedAt = folder.UpdatedAt,
      FilesCount = 0
    };

    return CreatedAtAction(nameof(GetFolders), new { id = folder.Id }, response);
  }

  /// <summary>
  /// Удалить папку
  /// </summary>
  /// <param name="id">Идентификатор папки</param>
  /// <returns>Результат операции</returns>
  /// <response code="204">Папка удалена</response>
  /// <response code="401">Не авторизован</response>
  /// <response code="403">Нет прав на удаление этой папки</response>
  /// <response code="404">Папка не найдена</response>
  /// <response code="409">Папка содержит файлы, удаление невозможно</response>
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

    var folder = await _folderRepository.GetByIdAsync(id);
    if (folder == null)
      return NotFound(new ErrorResponse { Error = "folder_not_found", ErrorDescription = "Folder not found." });

    if (folder.OwnerId != userId.Value)
      return Forbid(); // или StatusCode(403) с телом

    var filesCount = await _folderRepository.GetFilesCountAsync(id);
    if (filesCount > 0)
    {
      return Conflict(new ErrorResponse
      {
        Error = "folder_not_empty",
        ErrorDescription = $"Folder contains {filesCount} file(s). Delete files first or confirm deletion."
      });
    }

    await _folderRepository.DeleteAsync(id);
    return NoContent();
  }

  private Guid? GetCurrentUserId()
  {
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (Guid.TryParse(userIdClaim, out var userId))
      return userId;
    return null;
  }
}
