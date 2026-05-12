using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Notifications.Models;
using GraNAS.Notifications.Models.Entities;
using GraNAS.Notifications.Models.Repositories;
using GraNAS.Notifications.Services.Implementations;
using GraNAS.Notifications.Services.Interfaces;
using GraNAS.Shared.Messaging.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GraNAS.WebAPI.Tests.Unit.Notifications;

public class NotificationIngestionServiceTests
{
    private readonly Mock<INotificationEventRepository>  _events          = new();
    private readonly Mock<INotificationOutboxRepository> _outbox          = new();
    private readonly Mock<IUserSettingsResolver>         _settingsResolver = new();
    private readonly Mock<IPushSubscriptionRepository>   _pushSubs        = new();
    private readonly NotificationIngestionService        _sut;

    public NotificationIngestionServiceTests()
    {
        _settingsResolver
            .Setup(r => r.GetPrefsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationPrefs.AllEnabled());
        _pushSubs
            .Setup(r => r.HasActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _sut = new NotificationIngestionService(
            _events.Object,
            _outbox.Object,
            _settingsResolver.Object,
            _pushSubs.Object,
            NullLogger<NotificationIngestionService>.Instance);
    }

    private static IntegrationEventEnvelope BuildEnvelope(Guid targetUserId, string eventType = "access.granted")
    {
        var payload = JsonDocument.Parse($@"{{""TargetUserId"":""{targetUserId}""}}").RootElement;
        return new IntegrationEventEnvelope(Guid.NewGuid(), eventType, DateTime.UtcNow, null, payload);
    }

    [Fact]
    public async Task Handle_NewEvent_InsertsEventAndTwoOutboxRows()
    {
        var userId = Guid.NewGuid();
        var envelope = BuildEnvelope(userId);

        _events.Setup(r => r.GetByEventIdAsync(envelope.EventId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationEvent?)null);
        _events.Setup(r => r.InsertAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationEvent e, CancellationToken _) => e);
        _outbox.Setup(r => r.InsertManyAsync(It.IsAny<System.Collections.Generic.IEnumerable<NotificationOutbox>>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        await _sut.HandleAsync(envelope);

        _events.Verify(r => r.InsertAsync(It.Is<NotificationEvent>(e =>
            e.EventId == envelope.EventId &&
            e.UserId == userId &&
            e.Type == "access.granted"), It.IsAny<CancellationToken>()), Times.Once);

        _outbox.Verify(r => r.InsertManyAsync(
            It.Is<System.Collections.Generic.IEnumerable<NotificationOutbox>>(rows =>
                System.Linq.Enumerable.Count(rows) == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEventId_IsIdempotent()
    {
        var userId = Guid.NewGuid();
        var envelope = BuildEnvelope(userId);

        _events.Setup(r => r.GetByEventIdAsync(envelope.EventId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new NotificationEvent { EventId = envelope.EventId });

        await _sut.HandleAsync(envelope);

        _events.Verify(r => r.InsertAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(r => r.InsertManyAsync(It.IsAny<System.Collections.Generic.IEnumerable<NotificationOutbox>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownUserId_Drops()
    {
        var payload = JsonDocument.Parse("{}").RootElement;
        var envelope = new IntegrationEventEnvelope(Guid.NewGuid(), "access.granted", DateTime.UtcNow, null, payload);

        await _sut.HandleAsync(envelope);

        _events.Verify(r => r.InsertAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShareRevokedNoTargetUserId_FallbacksToOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var payload = JsonDocument.Parse($@"{{""OwnerId"":""{ownerId}""}}").RootElement;
        var envelope = new IntegrationEventEnvelope(Guid.NewGuid(), "share.revoked", DateTime.UtcNow, null, payload);

        _events.Setup(r => r.GetByEventIdAsync(envelope.EventId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationEvent?)null);
        _events.Setup(r => r.InsertAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationEvent e, CancellationToken _) => e);
        _outbox.Setup(r => r.InsertManyAsync(It.IsAny<System.Collections.Generic.IEnumerable<NotificationOutbox>>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        await _sut.HandleAsync(envelope);

        _events.Verify(r => r.InsertAsync(
            It.Is<NotificationEvent>(e => e.UserId == ownerId && e.Type == "share.revoked"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailDisabledInPrefs_DoesNotCreateEmailOutbox()
    {
        var userId = Guid.NewGuid();
        var envelope = BuildEnvelope(userId);

        var prefs = new NotificationPrefs
        {
            Email   = new NotificationChannelPrefs(false),
            InApp   = new NotificationChannelPrefs(true),
            WebPush = new NotificationChannelPrefs(false),
        };
        _settingsResolver.Setup(r => r.GetPrefsAsync(userId, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(prefs);

        _events.Setup(r => r.GetByEventIdAsync(envelope.EventId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationEvent?)null);
        _events.Setup(r => r.InsertAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationEvent e, CancellationToken _) => e);
        _outbox.Setup(r => r.InsertManyAsync(It.IsAny<System.Collections.Generic.IEnumerable<NotificationOutbox>>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        await _sut.HandleAsync(envelope);

        _outbox.Verify(r => r.InsertManyAsync(
            It.Is<System.Collections.Generic.IEnumerable<NotificationOutbox>>(rows =>
                System.Linq.Enumerable.All(rows, r => r.Target != DeliveryTarget.Email)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AllChannelsDisabled_DoesNotCreateNotificationEvent()
    {
        var userId = Guid.NewGuid();
        var envelope = BuildEnvelope(userId);

        var prefs = new NotificationPrefs
        {
            Email   = new NotificationChannelPrefs(false),
            InApp   = new NotificationChannelPrefs(false),
            WebPush = new NotificationChannelPrefs(false),
        };
        _settingsResolver.Setup(r => r.GetPrefsAsync(userId, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(prefs);

        _events.Setup(r => r.GetByEventIdAsync(envelope.EventId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationEvent?)null);

        await _sut.HandleAsync(envelope);

        _events.Verify(r => r.InsertAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WebPushEnabledButNoSubscriptions_DoesNotCreateWebPushOutbox()
    {
        var userId = Guid.NewGuid();
        var envelope = BuildEnvelope(userId);

        // webPush enabled in prefs but no active subscriptions
        _pushSubs.Setup(r => r.HasActiveAsync(userId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        _events.Setup(r => r.GetByEventIdAsync(envelope.EventId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationEvent?)null);
        _events.Setup(r => r.InsertAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationEvent e, CancellationToken _) => e);
        _outbox.Setup(r => r.InsertManyAsync(It.IsAny<System.Collections.Generic.IEnumerable<NotificationOutbox>>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        await _sut.HandleAsync(envelope);

        _outbox.Verify(r => r.InsertManyAsync(
            It.Is<System.Collections.Generic.IEnumerable<NotificationOutbox>>(rows =>
                System.Linq.Enumerable.All(rows, r => r.Target != DeliveryTarget.WebPush)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
