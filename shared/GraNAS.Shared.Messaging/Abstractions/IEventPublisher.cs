using System.Threading;
using System.Threading.Tasks;

namespace GraNAS.Shared.Messaging.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default)
        where TEvent : IIntegrationEvent;
}
