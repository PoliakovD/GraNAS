using GraNAS.Desktop.Contracts.Metadata;

namespace GraNAS.Desktop.App.Services.Api;

public class PermissionsApi : ApiBase, IPermissionsApi
{
  public PermissionsApi(HttpClient http) : base(http) { }

  public Task<List<PermissionResponse>> GetPermissionsAsync(Guid folderId, CancellationToken ct = default)
    => GetAsync<List<PermissionResponse>>($"api/metadata/folders/{folderId}/permissions", ct);

  public Task<PermissionResponse> GrantAsync(Guid folderId, GrantPermissionRequest request, CancellationToken ct = default)
    => PostAsync<PermissionResponse>($"api/metadata/folders/{folderId}/permissions", request, ct);

  public Task RevokeAsync(Guid folderId, Guid userId, CancellationToken ct = default)
    => DeleteAsync($"api/metadata/folders/{folderId}/permissions/{userId}", ct);
}
