using GraNAS.Sharing.Models.DTO;

namespace GraNAS.Sharing.Services.Interfaces;

public interface IMetadataServiceClient
{
    Task<FolderInfo?> GetFolderForOwnerAsync(Guid folderId, Guid ownerId, CancellationToken ct = default);
    Task<FolderInfo?> GetFolderAsync(Guid folderId, CancellationToken ct = default);
}
