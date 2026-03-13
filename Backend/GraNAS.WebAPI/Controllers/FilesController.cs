using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Models;
using GraNAS.Models.DTO;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GraNAS.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FilesController : ControllerBase
{
    private readonly IFileRepository _fileRepository;
    private readonly IFolderRepository _folderRepository;

    public FilesController(IFileRepository fileRepository, IFolderRepository folderRepository)
    {
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
    }

    /// <summary>
    /// Загрузить метаданные нового файла в указанную папку
    /// </summary>
    /// <param name="request">Данные файла</param>
    /// <returns>Созданный файл</returns>
    /// <response code="201">Файл успешно создан</response>
    /// <response code="400">Невалидные данные</response>
    /// <response code="401">Не авторизован</response>
    /// <response code="403">Нет прав на запись в эту папку</response>
    /// <response code="404">Папка не найдена</response>
    [HttpPost]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateFile([FromBody] CreateFileRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

        // Проверяем существование папки и её принадлежность пользователю
        var folder = await _folderRepository.GetByIdAsync(request.FolderId);
        if (folder == null)
            return NotFound(new ErrorResponse { Error = "folder_not_found", ErrorDescription = "Folder not found." });

        if (folder.OwnerId != userId.Value)
            return Forbid();

        var file = new File
        {
            Id = Guid.NewGuid(),
            FolderId = request.FolderId,
            OwnerId = userId.Value,
            Name = request.Name,
            Type = request.Type,
            Size = request.Size,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        await _fileRepository.CreateAsync(file);

        var response = new FileResponse
        {
            Id = file.Id,
            FolderId = file.FolderId,
            Name = file.Name,
            Type = file.Type,
            Size = file.Size,
            CreatedAt = file.CreatedAt,
            UpdatedAt = file.UpdatedAt
        };

        return CreatedAtAction(nameof(GetFile), new { id = file.Id }, response);
    }

    /// <summary>
    /// Получить информацию о файле по ID
    /// </summary>
    /// <param name="id">Идентификатор файла</param>
    /// <returns>Информация о файле</returns>
    /// <response code="200">Успешно</response>
    /// <response code="401">Не авторизован</response>
    /// <response code="403">Нет доступа к файлу</response>
    /// <response code="404">Файл не найден</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null)
            return NotFound(new ErrorResponse { Error = "file_not_found", ErrorDescription = "File not found." });

        if (file.OwnerId != userId.Value)
            return Forbid();

        var response = new FileResponse
        {
            Id = file.Id,
            FolderId = file.FolderId,
            Name = file.Name,
            Type = file.Type,
            Size = file.Size,
            CreatedAt = file.CreatedAt,
            UpdatedAt = file.UpdatedAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Удалить файл (метаданные)
    /// </summary>
    /// <param name="id">Идентификатор файла</param>
    /// <returns>Результат операции</returns>
    /// <response code="204">Файл удалён</response>
    /// <response code="401">Не авторизован</response>
    /// <response code="403">Нет прав на удаление</response>
    /// <response code="404">Файл не найден</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "unauthorized", ErrorDescription = "User not identified." });

        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null)
            return NotFound(new ErrorResponse { Error = "file_not_found", ErrorDescription = "File not found." });

        if (file.OwnerId != userId.Value)
            return Forbid();

        await _fileRepository.DeleteAsync(id);
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
