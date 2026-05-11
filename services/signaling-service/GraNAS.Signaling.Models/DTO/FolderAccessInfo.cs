namespace GraNAS.Signaling.Models.DTO;

/// <summary>
/// Информация о доступе к папке, полученная от metadata-service.
/// Используется в <see cref="GraNAS.Signaling.Services.Interfaces.IMetadataServiceClient"/> для проверки прав перед установкой P2P-сессии.
/// </summary>
/// <param name="FolderId">Идентификатор папки.</param>
/// <param name="OwnerId">Идентификатор владельца папки.</param>
/// <param name="ScopePath">
/// Путь-подсказка для ограничения доступа до конкретной вложенной папки или файла.
/// <c>null</c> означает доступ ко всей папке. Сервер не валидирует существование пути —
/// scope применяется owner-ом при P2P-handshake.
/// </param>
public record FolderAccessInfo(Guid FolderId, Guid OwnerId, string? ScopePath);
