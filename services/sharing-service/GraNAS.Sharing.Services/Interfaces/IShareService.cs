using GraNAS.Sharing.Models;
using GraNAS.Sharing.Models.DTO;

namespace GraNAS.Sharing.Services.Interfaces;

public interface IShareService
{
    Task<CreateShareResult> CreateAsync(Guid ownerId, Guid folderId, CreateShareRequest request, CancellationToken ct = default);
    Task<ShareDetailsResponse?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<bool> IsRevokedAsync(string token);
    Task<IEnumerable<ShareLinkResponse>?> ListByFolderAsync(Guid ownerId, Guid folderId, CancellationToken ct = default);
    Task<IEnumerable<ShareLinkOwnerResponse>> ListByOwnerAsync(Guid ownerId, bool activeOnly, int take, CancellationToken ct);
    Task<RevokeShareResult> RevokeByTokenAsync(Guid ownerId, string token);
    Task<RevokeShareResult> RevokeByIdAsync(Guid ownerId, Guid id);
    Task<int> DeleteExpiredAsync(CancellationToken ct = default);
    Task<ShareLink?> GetByTokenHashInternalAsync(string tokenHash, CancellationToken ct = default);
}
