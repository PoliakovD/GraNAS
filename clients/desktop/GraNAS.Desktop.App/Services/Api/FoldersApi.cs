using GraNAS.Desktop.Contracts.Metadata;

namespace GraNAS.Desktop.App.Services.Api;

public class FoldersApi : ApiBase, IFoldersApi
{
  public FoldersApi(HttpClient http) : base(http) { }

  public Task<List<FolderResponse>> GetFoldersAsync(CancellationToken ct = default)
    => GetAsync<List<FolderResponse>>("api/metadata/folders", ct);

  public Task<FolderResponse> CreateFolderAsync(CreateFolderRequest request, CancellationToken ct = default)
    => PostAsync<FolderResponse>("api/metadata/folders", request, ct);

  public Task DeleteFolderAsync(Guid id, CancellationToken ct = default)
    => DeleteAsync($"api/metadata/folders/{id}", ct);
}
