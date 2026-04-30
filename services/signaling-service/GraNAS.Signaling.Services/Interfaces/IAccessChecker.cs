namespace GraNAS.Signaling.Services.Interfaces;

public record FolderAccessResult(Guid FolderId, Guid OwnerId, string? ScopePath);

public interface IAccessChecker
{
    Task<FolderAccessResult?> CheckJwtAccessAsync(Guid folderId, Guid userId, CancellationToken ct = default);
    Task<FolderAccessResult?> CheckShareTokenAsync(Guid folderId, string shareToken, CancellationToken ct = default);
}
