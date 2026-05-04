using System.Text.Json;
using GraNAS.Notifications.Models;
using GraNAS.Notifications.Models.Entities;
using GraNAS.Notifications.Models.Repositories;
using GraNAS.Notifications.Services.Interfaces;
using GraNAS.Shared.Messaging.Events;
using Microsoft.Extensions.Logging;

namespace GraNAS.Notifications.Services.Implementations;

public class NotificationIngestionService : INotificationIngestionService
{
    private readonly INotificationEventRepository _events;
    private readonly INotificationOutboxRepository _outbox;
    private readonly ILogger<NotificationIngestionService> _logger;

    public NotificationIngestionService(
        INotificationEventRepository events,
        INotificationOutboxRepository outbox,
        ILogger<NotificationIngestionService> logger)
    {
        _events = events;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken ct = default)
    {
        _logger.LogDebug("Ingest: received {EventType} eventId={EventId}", envelope.EventType, envelope.EventId);

        var userId = ExtractUserId(envelope);
        if (userId == Guid.Empty)
        {
            _logger.LogWarning("Ingest: cannot determine userId for {EventType} eventId={EventId} — dropping",
                envelope.EventType, envelope.EventId);
            return;
        }

        var existing = await _events.GetByEventIdAsync(envelope.EventId, ct);
        if (existing is not null)
        {
            _logger.LogDebug("Ingest: duplicate eventId {EventId} — skipping", envelope.EventId);
            return;
        }

        var now = DateTime.UtcNow;
        var notificationEvent = new NotificationEvent
        {
            Id = Guid.NewGuid(),
            EventId = envelope.EventId,
            UserId = userId,
            Type = envelope.EventType,
            Data = JsonDocument.Parse(envelope.Payload.GetRawText()),
            CorrelationId = envelope.CorrelationId,
            IsRead = false,
            CreatedAt = now
        };

        try
        {
            await _events.InsertAsync(notificationEvent, ct);
            await _outbox.InsertManyAsync(new[]
            {
                new NotificationOutbox
                {
                    Id = Guid.NewGuid(),
                    NotificationEventId = notificationEvent.Id,
                    Target = DeliveryTarget.Email,
                    Status = OutboxStatus.Pending,
                    AttemptCount = 0,
                    NextAttemptAt = now,
                    CreatedAt = now
                },
                new NotificationOutbox
                {
                    Id = Guid.NewGuid(),
                    NotificationEventId = notificationEvent.Id,
                    Target = DeliveryTarget.SignalR,
                    Status = OutboxStatus.Pending,
                    AttemptCount = 0,
                    NextAttemptAt = now,
                    CreatedAt = now
                }
            }, ct);

            _logger.LogInformation("Ingest: stored {EventType} for user {UserId} (id={NotificationId})",
                envelope.EventType, userId, notificationEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingest: failed to persist {EventType} eventId={EventId}",
                envelope.EventType, envelope.EventId);
            throw;
        }
    }

    private static Guid ExtractUserId(IntegrationEventEnvelope envelope)
    {
        try
        {
            if (envelope.Payload.TryGetProperty("targetUserId", out var prop) && prop.ValueKind == JsonValueKind.String)
                return Guid.Parse(prop.GetString()!);
            if (envelope.Payload.TryGetProperty("TargetUserId", out prop) && prop.ValueKind == JsonValueKind.String)
                return Guid.Parse(prop.GetString()!);
        }
        catch { /* ignore parse failures */ }
        return Guid.Empty;
    }
}
