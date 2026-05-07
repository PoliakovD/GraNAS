using GraNAS.Notifications.Models.Entities;
using GraNAS.Notifications.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Notifications.DAL.Repositories;

public class NotificationEventRepository : INotificationEventRepository
{
    private readonly NotificationDbContext _context;

    public NotificationEventRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public Task<NotificationEvent?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default)
        => _context.NotificationEvents.FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public async Task<NotificationEvent> InsertAsync(NotificationEvent evt, CancellationToken ct = default)
    {
        _context.NotificationEvents.Add(evt);
        await _context.SaveChangesAsync(ct);
        return evt;
    }

    public async Task<IReadOnlyList<NotificationEvent>> ListByUserAsync(
        Guid userId, DateTime? cursor, int take, CancellationToken ct = default)
    {
        var query = _context.NotificationEvents
            .Where(e => e.UserId == userId);

        if (cursor.HasValue)
            query = query.Where(e => e.CreatedAt < cursor.Value);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
        => _context.NotificationEvents.CountAsync(e => e.UserId == userId && !e.IsRead, ct);

    public async Task<bool> MarkReadAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var evt = await _context.NotificationEvents
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (evt is null) return false;

        evt.IsRead = true;
        evt.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.NotificationEvents
            .Where(e => e.UserId == userId && !e.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.IsRead, true)
                .SetProperty(e => e.ReadAt, DateTime.UtcNow), ct);
    }

    public async Task<IReadOnlyList<NotificationEvent>> GetUnreadByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.NotificationEvents
            .Where(e => e.UserId == userId && !e.IsRead)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);
    }
}
