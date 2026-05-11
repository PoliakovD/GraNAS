namespace GraNAS.Signaling.Models.DTO;

/// <summary>
/// Информация о share-ссылке, полученная от sharing-service по хешу токена.
/// Используется в <see cref="GraNAS.Signaling.Services.Interfaces.ISharingServiceClient"/>
/// для проверки доступа анонимных пользователей при запросе P2P-сессии.
/// </summary>
/// <param name="FolderId">Папка, к которой открывает доступ ссылка.</param>
/// <param name="OwnerId">Идентификатор владельца папки.</param>
/// <param name="ScopePath">Ограничение пути. <c>null</c> = вся папка.</param>
/// <param name="ExpiresAt">Момент истечения ссылки. Проверяется в <c>AccessChecker</c>.</param>
/// <param name="Revoked"><c>true</c>, если ссылка была отозвана владельцем вручную.</param>
public record ShareInfo(
    Guid FolderId,
    Guid OwnerId,
    string? ScopePath,
    DateTime ExpiresAt,
    bool Revoked);
