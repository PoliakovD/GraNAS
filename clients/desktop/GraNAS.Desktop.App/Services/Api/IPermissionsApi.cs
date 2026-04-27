using GraNAS.Desktop.Contracts.Metadata;

namespace GraNAS.Desktop.App.Services.Api;

public interface IPermissionsApi
{
  Task<List<PermissionResponse>> GetPermissionsAsync(Guid folderId, CancellationToken ct = default);
  Task<PermissionResponse> GrantAsync(Guid folderId, GrantPermissionRequest request, CancellationToken ct = default);
  Task RevokeAsync(Guid folderId, Guid userId, CancellationToken ct = default);
}
