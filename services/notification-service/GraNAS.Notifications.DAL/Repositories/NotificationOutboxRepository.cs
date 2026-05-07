using GraNAS.Notifications.Models;
using GraNAS.Notifications.Models.Entities;
using GraNAS.Notifications.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Notifications.DAL.Repositories;

public class NotificationOutboxRepository : INotificationOutboxRepository
{
    private readonly NotificationDbContext _context;

    public NotificationOutboxRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task InsertManyAsync(IEnumerable<NotificationOutbox> rows, CancellationToken ct = default)
    {
        _context.NotificationOutbox.AddRange(rows);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationOutbox>> ClaimDueBatchAsync(
        DeliveryTarget target, int batch, DateTime now, CancellationToken ct = default)
    {
        var targetStr = target.ToString();
        var statusStr = OutboxStatus.Pending.ToString();

        return await _context.NotificationOutbox
            .FromSqlRaw("""
                SELECT * FROM table_notification_outbox
                WHERE target = {0} AND status = {1} AND next_attempt_at <= {2}
                ORDER BY next_attempt_at
                LIMIT {3}
                FOR UPDATE SKIP LOCKED
                """, targetStr, statusStr, now, batch)
            .Include(o => o.NotificationEvent)
            .ToListAsync(ct);
    }

    public async Task MarkDeliveredAsync(Guid id, CancellationToken ct = default)
    {
        await _context.NotificationOutbox
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OutboxStatus.Delivered)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow), ct);
    }

    public async Task MarkRetryAsync(Guid id, DateTime nextAttemptAt, string error, CancellationToken ct = default)
    {
        await _context.NotificationOutbox
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.AttemptCount, o => o.AttemptCount + 1)
                .SetProperty(o => o.NextAttemptAt, nextAttemptAt)
                .SetProperty(o => o.LastError, error)
                .SetProperty(o => o.LastAttemptAt, DateTime.UtcNow)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow), ct);
    }

    public async Task MarkPermanentFailureAsync(Guid id, string error, CancellationToken ct = default)
    {
        await _context.NotificationOutbox
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OutboxStatus.Failed)
                .SetProperty(o => o.AttemptCount, o => o.AttemptCount + 1)
                .SetProperty(o => o.LastError, error)
                .SetProperty(o => o.LastAttemptAt, DateTime.UtcNow)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow), ct);
    }
}
