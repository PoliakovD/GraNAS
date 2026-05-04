namespace GraNAS.Signaling.Models.DTO;

public record ShareInfo(
    Guid FolderId,
    Guid OwnerId,
    string? ScopePath,
    DateTime ExpiresAt,
    bool Revoked);
