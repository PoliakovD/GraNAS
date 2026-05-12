using GraNAS.Notifications.Models.Entities;

namespace GraNAS.Notifications.Models.Repositories;

public interface IPushSubscriptionRepository
{
    Task UpsertAsync(PushSubscription sub, CancellationToken ct = default);
    Task DeleteByEndpointAsync(string endpoint, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PushSubscription>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasActiveAsync(Guid userId, CancellationToken ct = default);
}
