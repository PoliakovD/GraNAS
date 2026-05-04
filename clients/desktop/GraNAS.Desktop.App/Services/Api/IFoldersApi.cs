using GraNAS.Desktop.Contracts.Metadata;

namespace GraNAS.Desktop.App.Services.Api;

public interface IFoldersApi
{
  Task<List<FolderResponse>> GetFoldersAsync(CancellationToken ct = default);
  Task<FolderResponse> CreateFolderAsync(CreateFolderRequest request, CancellationToken ct = default);
  Task DeleteFolderAsync(Guid id, CancellationToken ct = default);
}
