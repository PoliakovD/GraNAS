using GraNAS.Signaling.Models.DTO;

namespace GraNAS.Signaling.Services.Interfaces;

/// <summary>HTTP-клиент к metadata-service для проверки прав доступа пользователя к папке.</summary>
public interface IMetadataServiceClient
{
    /// <summary>
    /// Запрашивает информацию о доступе пользователя к папке у metadata-service
    /// (<c>GET /api/internal/folders/{folderId}/access</c>).
    /// </summary>
    /// <param name="folderId">Идентификатор папки.</param>
    /// <param name="userId">Идентификатор пользователя, права которого проверяются.</param>
    /// <returns>Информация о доступе или <c>null</c>, если папка не найдена или доступ отсутствует (HTTP 404).</returns>
    Task<FolderAccessInfo?> GetFolderAccessAsync(Guid folderId, Guid userId, CancellationToken ct = default);
}
