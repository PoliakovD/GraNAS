using GraNAS.Desktop.Contracts.Sharing;

namespace GraNAS.Desktop.App.Services.Api;

public class SharesApi : ApiBase, ISharesApi
{
  public SharesApi(HttpClient http) : base(http) { }

  public Task<List<ShareLinkResponse>> GetSharesAsync(Guid folderId, CancellationToken ct = default)
    => GetAsync<List<ShareLinkResponse>>($"api/sharing/folders/{folderId}/shares", ct);

  public Task<CreateShareResponse> CreateShareAsync(Guid folderId, CreateShareRequest request, CancellationToken ct = default)
    => PostAsync<CreateShareResponse>($"api/sharing/folders/{folderId}/share", request, ct);

  public Task RevokeShareAsync(Guid shareLinkId, CancellationToken ct = default)
    => DeleteAsync($"api/sharing/share-links/{shareLinkId}", ct);

  public Task<ShareDetailsResponse> GetShareDetailsAsync(string token, CancellationToken ct = default)
    => GetAsync<ShareDetailsResponse>($"api/sharing/share/{token}", ct);

  public async Task<List<ShareLinkOwnerResponse>> ListAllSharesAsync(bool activeOnly = true, CancellationToken ct = default)
  {
    try { return await GetAsync<List<ShareLinkOwnerResponse>>($"api/share-links?activeOnly={activeOnly}", ct); }
    catch { return []; }
  }
}
