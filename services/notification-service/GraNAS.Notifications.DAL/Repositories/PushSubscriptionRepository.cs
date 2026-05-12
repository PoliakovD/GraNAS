using GraNAS.Notifications.Models.Entities;
using GraNAS.Notifications.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Notifications.DAL.Repositories;

public class PushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly NotificationDbContext _context;

    public PushSubscriptionRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task UpsertAsync(PushSubscription sub, CancellationToken ct = default)
    {
        var existing = await _context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == sub.Endpoint, ct);

        if (existing is null)
        {
            _context.PushSubscriptions.Add(sub);
        }
        else
        {
            existing.UserId    = sub.UserId;
            existing.P256dh    = sub.P256dh;
            existing.Auth      = sub.Auth;
            existing.UserAgent = sub.UserAgent;
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteByEndpointAsync(string endpoint, Guid userId, CancellationToken ct = default)
    {
        var sub = await _context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId, ct);
        if (sub is not null)
        {
            _context.PushSubscriptions.Remove(sub);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<PushSubscription>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await _context.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync(ct);

    public async Task<bool> HasActiveAsync(Guid userId, CancellationToken ct = default)
        => await _context.PushSubscriptions.AnyAsync(s => s.UserId == userId, ct);
}
