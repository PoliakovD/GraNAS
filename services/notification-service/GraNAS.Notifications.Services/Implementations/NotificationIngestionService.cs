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
    private readonly IUserSettingsResolver _settingsResolver;
    private readonly IPushSubscriptionRepository _pushSubs;
    private readonly ILogger<NotificationIngestionService> _logger;

    public NotificationIngestionService(
        INotificationEventRepository events,
        INotificationOutboxRepository outbox,
        IUserSettingsResolver settingsResolver,
        IPushSubscriptionRepository pushSubs,
        ILogger<NotificationIngestionService> logger)
    {
        _events = events;
        _outbox = outbox;
        _settingsResolver = settingsResolver;
        _pushSubs = pushSubs;
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

        var prefs = await _settingsResolver.GetPrefsAsync(userId, ct);
        var inAppEnabled  = prefs.IsEnabled(DeliveryTarget.SignalR, envelope.EventType);
        var emailEnabled  = prefs.IsEnabled(DeliveryTarget.Email,   envelope.EventType);
        var webPushEnabled = prefs.IsEnabled(DeliveryTarget.WebPush, envelope.EventType);

        if (!inAppEnabled && !emailEnabled && !webPushEnabled)
        {
            _logger.LogInformation("Ingest: all channels disabled for user {UserId} type {EventType} — skipping",
                userId, envelope.EventType);
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

        var outboxRows = new List<NotificationOutbox>();

        if (emailEnabled)
            outboxRows.Add(new NotificationOutbox
            {
                Id = Guid.NewGuid(),
                NotificationEventId = notificationEvent.Id,
                Target = DeliveryTarget.Email,
                Status = OutboxStatus.Pending,
                AttemptCount = 0,
                NextAttemptAt = now,
                CreatedAt = now
            });

        if (inAppEnabled)
            outboxRows.Add(new NotificationOutbox
            {
                Id = Guid.NewGuid(),
                NotificationEventId = notificationEvent.Id,
                Target = DeliveryTarget.SignalR,
                Status = OutboxStatus.Pending,
                AttemptCount = 0,
                NextAttemptAt = now,
                CreatedAt = now
            });

        if (webPushEnabled && await _pushSubs.HasActiveAsync(userId, ct))
            outboxRows.Add(new NotificationOutbox
            {
                Id = Guid.NewGuid(),
                NotificationEventId = notificationEvent.Id,
                Target = DeliveryTarget.WebPush,
                Status = OutboxStatus.Pending,
                AttemptCount = 0,
                NextAttemptAt = now,
                CreatedAt = now
            });

        try
        {
            await _events.InsertAsync(notificationEvent, ct);
            if (outboxRows.Count > 0)
                await _outbox.InsertManyAsync(outboxRows, ct);

            _logger.LogInformation("Ingest: stored {EventType} for user {UserId} (id={NotificationId}, outboxRows={OutboxCount})",
                envelope.EventType, userId, notificationEvent.Id, outboxRows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingest: failed to persist {EventType} eventId={EventId}",
                envelope.EventType, envelope.EventId);
            throw;
        }
    }

    private Guid ExtractUserId(IntegrationEventEnvelope envelope)
    {
        try
        {
            if (envelope.Payload.TryGetProperty("targetUserId", out var prop) && prop.ValueKind == JsonValueKind.String)
                return Guid.Parse(prop.GetString()!);
            if (envelope.Payload.TryGetProperty("TargetUserId", out prop) && prop.ValueKind == JsonValueKind.String)
                return Guid.Parse(prop.GetString()!);

            // share.revoked has no TargetUserId — fall back to OwnerId as self-confirmation recipient
            if (envelope.EventType == "share.revoked")
            {
                if (envelope.Payload.TryGetProperty("ownerId", out prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var id = Guid.Parse(prop.GetString()!);
                    _logger.LogDebug("Ingest: share.revoked fallback to ownerId {OwnerId}", id);
                    return id;
                }
                if (envelope.Payload.TryGetProperty("OwnerId", out prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var id = Guid.Parse(prop.GetString()!);
                    _logger.LogDebug("Ingest: share.revoked fallback to OwnerId {OwnerId}", id);
                    return id;
                }
            }
        }
        catch { /* ignore parse failures */ }
        return Guid.Empty;
    }
}
