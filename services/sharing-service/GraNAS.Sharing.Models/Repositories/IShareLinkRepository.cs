namespace GraNAS.Sharing.Models.Repositories;

public interface IShareLinkRepository
{
    Task CreateAsync(ShareLink shareLink);
    Task<ShareLink?> GetByTokenHashAsync(string tokenHash);
    Task<ShareLink?> GetByIdForOwnerAsync(Guid id, Guid ownerId);
    Task<IEnumerable<ShareLink>> ListByFolderForOwnerAsync(Guid folderId, Guid ownerId);
    Task<IEnumerable<ShareLink>> ListByOwnerAsync(Guid ownerId, bool activeOnly, int take, CancellationToken ct);
    Task UpdateAsync(ShareLink shareLink);
    Task<int> DeleteExpiredAsync(DateTime cutoff);
}
