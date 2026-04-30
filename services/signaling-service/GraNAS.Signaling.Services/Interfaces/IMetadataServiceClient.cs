using GraNAS.Signaling.Models.DTO;

namespace GraNAS.Signaling.Services.Interfaces;

public interface IMetadataServiceClient
{
    Task<FolderAccessInfo?> GetFolderAccessAsync(Guid folderId, Guid userId, CancellationToken ct = default);
}
