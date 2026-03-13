using System;
using System.Collections.Generic;
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

namespace GraNAS.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FoldersController : ControllerBase
{
  private readonly IFolderRepository _folderRepository;
  private readonly IFileRepository _fileRepository;

  public FoldersController(IFolderRepository folderRepository, IFileRepository fileRepository)
  {
    _folderRepository = folderRepository;
    _fileRepository = fileRepository;
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
    var response = new List<FolderResponse>();
    foreach (var f in folders)
    {
      var filesCount = await _folderRepository.GetFilesCountAsync(f.Id);
      var subfoldersCount = await _folderRepository.GetSubfoldersCountAsync(f.Id);
      response.Add(new FolderResponse
      {
        Id = f.Id,
        Name = f.Name,
        ParentId = f.ParentId,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt,
        FilesCount = filesCount,
        SubfoldersCount = subfoldersCount
      });
    }
    return Ok(response);
  }

  /// <summary>
  /// Получить содержимое папки (подпапки и файлы)
  /// </summary>
  /// <param name="folderId">Идентификатор папки</param>
  [HttpGet("{folderId}/contents")]
  [ProducesResponseType(typeof(FolderContentsResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetFolderContents(Guid folderId)
  {
    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var folder = await _folderRepository.GetByIdAsync(folderId);
    if (folder == null)
      return NotFound(new ErrorResponse { Error = "folder_not_found", ErrorDescription = "Folder not found." });

    if (folder.OwnerId != userId.Value)
      return Forbid();

    var subfolders = await _folderRepository.GetChildFoldersAsync(folderId);
    var files = await _fileRepository.GetFilesInFolderAsync(folderId);

    // Для каждой подпапки можно заполнить FilesCount и SubfoldersCount, но это увеличит число запросов.
    // В данном примере оставляем 0 для простоты (или можно добавить, если нужно).
    var subfolderResponses = subfolders.Select( f => new FolderResponse
    {
      Id = f.Id,
      Name = f.Name,
      ParentId = f.ParentId,
      CreatedAt = f.CreatedAt,
      UpdatedAt = f.UpdatedAt,
      FilesCount = 0, // или await _folderRepository.GetFilesCountAsync(f.Id)
      SubfoldersCount =  0 //   илиawait _folderRepository.GetSubfoldersCountAsync(f.Id)
    });

    var fileResponses = files.Select(f => new FileResponse
    {
      Id = f.Id,
      FolderId = f.FolderId,
      Name = f.Name,
      Type = f.Type,
      Size = f.Size,
      CreatedAt = f.CreatedAt,
      UpdatedAt = f.UpdatedAt
    });

    var response = new FolderContentsResponse
    {
      Subfolders = subfolderResponses,
      Files = fileResponses
    };

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
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

        // Если указан ParentId, проверяем, что родительская папка существует и принадлежит пользователю
        if (request.ParentId.HasValue)
        {
            var parentFolder = await _folderRepository.GetByIdAsync(request.ParentId.Value);
            if (parentFolder == null)
                return NotFound(new ErrorResponse { Error = "parent_folder_not_found", ErrorDescription = "Parent folder not found." });

            if (parentFolder.OwnerId != userId.Value)
                return Forbid();
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            OwnerId = userId.Value,
            ParentId = request.ParentId,
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        await _folderRepository.CreateAsync(folder);

        var response = new FolderResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            ParentId = folder.ParentId,
            CreatedAt = folder.CreatedAt,
            UpdatedAt = folder.UpdatedAt,
            FilesCount = 0,
            SubfoldersCount = 0
        };

        return CreatedAtAction(nameof(GetFolderContents), new { folderId = folder.Id }, response);
    }

  /// <summary>
  /// Удалить папку только если пустая
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
      return Forbid();

    // Проверяем, есть ли файлы или подпапки
    var filesCount = await _folderRepository.GetFilesCountAsync(id);
    var hasSubfolders = await _folderRepository.HasSubfoldersAsync(id);
    if (filesCount > 0 || hasSubfolders)
    {
      return Conflict(new ErrorResponse
      {
        Error = "folder_not_empty",
        ErrorDescription = $"Folder contains {filesCount} file(s) and {(hasSubfolders ? "subfolders" : "no subfolders")}. Cannot delete."
      });
    }

    await _folderRepository.DeleteAsync(id);
    return NoContent();
  }

  /// <summary>
  /// Получить список файлов в указанной папке
  /// </summary>
  /// <param name="folderId">Идентификатор папки</param>
  /// <returns>Список файлов</returns>
  /// <response code="200">Успешно</response>
  /// <response code="401">Не авторизован</response>
  /// <response code="403">Нет доступа к папке</response>
  /// <response code="404">Папка не найдена</response>
  [HttpGet("{folderId}/files")]
  [ProducesResponseType(typeof(FileResponse[]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetFilesInFolder(Guid folderId)
  {
    var userId = GetCurrentUserId();
    if (userId == null)
      return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

    var folder = await _folderRepository.GetByIdAsync(folderId);
    if (folder == null)
      return NotFound(new ErrorResponse { Error = "folder_not_found", ErrorDescription = "Folder not found." });

    if (folder.OwnerId != userId.Value)
      return Forbid();

    var files = await _fileRepository.GetFilesInFolderAsync(folderId);
    var response = files.Select(f => new FileResponse
    {
      Id = f.Id,
      FolderId = f.FolderId,
      Name = f.Name,
      Type = f.Type,
      Size = f.Size,
      CreatedAt = f.CreatedAt,
      UpdatedAt = f.UpdatedAt
    }).ToArray();

    return Ok(response);
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
