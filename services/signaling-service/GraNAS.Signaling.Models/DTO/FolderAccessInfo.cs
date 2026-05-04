namespace GraNAS.Signaling.Models.DTO;

public record FolderAccessInfo(Guid FolderId, Guid OwnerId, string? ScopePath);
