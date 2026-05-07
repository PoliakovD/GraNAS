using GraNAS.Notifications.Models.Entities;

namespace GraNAS.Notifications.Models.Repositories;

public interface INotificationOutboxRepository
{
    Task InsertManyAsync(IEnumerable<NotificationOutbox> rows, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationOutbox>> ClaimDueBatchAsync(DeliveryTarget target, int batch, DateTime now, CancellationToken ct = default);
    Task MarkDeliveredAsync(Guid id, CancellationToken ct = default);
    Task MarkRetryAsync(Guid id, DateTime nextAttemptAt, string error, CancellationToken ct = default);
    Task MarkPermanentFailureAsync(Guid id, string error, CancellationToken ct = default);
}
