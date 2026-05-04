using System;

namespace GraNAS.Shared.Messaging.Abstractions;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
}
