using GraNAS.Shared.Messaging.Events;

namespace GraNAS.Notifications.Services.Interfaces;

public interface INotificationIngestionService
{
    Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken ct = default);
}
