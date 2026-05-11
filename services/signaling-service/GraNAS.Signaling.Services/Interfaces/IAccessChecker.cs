namespace GraNAS.Signaling.Services.Interfaces;

/// <summary>
/// Результат проверки доступа к папке.
/// Возвращается при успешной авторизации; <c>null</c> означает отказ в доступе.
/// </summary>
/// <param name="FolderId">Идентификатор папки, к которой разрешён доступ.</param>
/// <param name="OwnerId">Идентификатор владельца папки — адресат P2P-запроса.</param>
/// <param name="ScopePath">Путь-подсказка для ограничения доступа. <c>null</c> = вся папка.</param>
public record FolderAccessResult(Guid FolderId, Guid OwnerId, string? ScopePath);

/// <summary>
/// Сервис проверки прав доступа к папке перед установкой P2P-сессии.
/// Поддерживает два режима авторизации: по JWT-токену зарегистрированного пользователя
/// и по share-токену для анонимного доступа.
/// </summary>
public interface IAccessChecker
{
    /// <summary>
    /// Проверяет, имеет ли зарегистрированный пользователь доступ к папке.
    /// Обращается к metadata-service через <see cref="IMetadataServiceClient"/>.
    /// </summary>
    /// <param name="folderId">Папка, к которой запрашивается доступ.</param>
    /// <param name="userId">Аутентифицированный пользователь из JWT-клейма.</param>
    /// <returns>Результат доступа или <c>null</c>, если доступ запрещён или папка не найдена.</returns>
    Task<FolderAccessResult?> CheckJwtAccessAsync(Guid folderId, Guid userId, CancellationToken ct = default);
    /// <summary>
    /// Проверяет, действителен ли share-токен для доступа к указанной папке.
    /// Вычисляет SHA-256 от токена и обращается к sharing-service.
    /// </summary>
    /// <param name="folderId">Папка, к которой запрашивается доступ.</param>
    /// <param name="shareToken">Сырой base64url-токен из запроса клиента.</param>
    /// <returns>Результат доступа или <c>null</c>, если токен недействителен, просрочен или отозван.</returns>
    Task<FolderAccessResult?> CheckShareTokenAsync(Guid folderId, string shareToken, CancellationToken ct = default);
}
