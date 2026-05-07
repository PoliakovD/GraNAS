using GraNAS.Notifications.Models.Entities;

namespace GraNAS.Notifications.Models.Repositories;

public interface INotificationEventRepository
{
    Task<NotificationEvent?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default);
    Task<NotificationEvent> InsertAsync(NotificationEvent evt, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationEvent>> ListByUserAsync(Guid userId, DateTime? cursor, int take, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationEvent>> GetUnreadByUserAsync(Guid userId, CancellationToken ct = default);
}
