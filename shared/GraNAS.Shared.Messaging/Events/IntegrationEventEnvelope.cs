using System;
using System.Text.Json;

namespace GraNAS.Shared.Messaging.Events;

public sealed record IntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    DateTime OccurredAt,
    string? CorrelationId,
    JsonElement Payload);
