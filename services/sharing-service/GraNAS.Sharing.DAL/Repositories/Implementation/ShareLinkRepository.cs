using GraNAS.Sharing.Models;
using GraNAS.Sharing.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Sharing.DAL.Repositories.Implementation;

public class ShareLinkRepository : IShareLinkRepository
{
    private readonly SharingDbContext _context;

    public ShareLinkRepository(SharingDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(ShareLink shareLink)
    {
        await _context.ShareLinks.AddAsync(shareLink);
        await _context.SaveChangesAsync();
    }

    public async Task<ShareLink?> GetByTokenHashAsync(string tokenHash)
    {
        return await _context.ShareLinks
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash);
    }

    public async Task<ShareLink?> GetByIdForOwnerAsync(Guid id, Guid ownerId)
    {
        return await _context.ShareLinks
            .FirstOrDefaultAsync(s => s.Id == id && s.OwnerId == ownerId);
    }

    public async Task<IEnumerable<ShareLink>> ListByFolderForOwnerAsync(Guid folderId, Guid ownerId)
    {
        return await _context.ShareLinks
            .Where(s => s.FolderId == folderId && s.OwnerId == ownerId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ShareLink>> ListByOwnerAsync(Guid ownerId, bool activeOnly, int take, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await _context.ShareLinks
            .Where(s => s.OwnerId == ownerId)
            .Where(s => !activeOnly || (!s.Revoked && s.ExpiresAt > now))
            .OrderByDescending(s => s.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(ShareLink shareLink)
    {
        _context.ShareLinks.Update(shareLink);
        await _context.SaveChangesAsync();
    }

    public async Task<int> DeleteExpiredAsync(DateTime cutoff)
    {
        return await _context.ShareLinks
            .Where(s => s.ExpiresAt < cutoff && !s.Revoked)
            .ExecuteDeleteAsync();
    }
}
