using GraNAS.Desktop.Contracts.Sharing;

namespace GraNAS.Desktop.App.Services.Api;

public interface ISharesApi
{
  Task<List<ShareLinkResponse>> GetSharesAsync(Guid folderId, CancellationToken ct = default);
  Task<CreateShareResponse> CreateShareAsync(Guid folderId, CreateShareRequest request, CancellationToken ct = default);
  Task RevokeShareAsync(Guid shareLinkId, CancellationToken ct = default);
  Task<ShareDetailsResponse> GetShareDetailsAsync(string token, CancellationToken ct = default);
  Task<List<ShareLinkOwnerResponse>> ListAllSharesAsync(bool activeOnly = true, CancellationToken ct = default);
}
