using System;
using GraNAS.Shared.Messaging.Abstractions;

namespace GraNAS.Shared.Messaging.Events;

public abstract record IntegrationEventBase : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public abstract string EventType { get; }
}
