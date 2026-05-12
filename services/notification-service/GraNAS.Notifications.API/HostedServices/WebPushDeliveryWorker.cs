using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using GraNAS.Notifications.Models;
using GraNAS.Notifications.Models.Repositories;
using GraNAS.Notifications.Services.Interfaces;
using GraNAS.Notifications.Services.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GraNAS.Notifications.API.HostedServices;

public class WebPushDeliveryWorker : BackgroundService
{
    private static readonly TimeSpan[] Backoffs =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6)
    ];

    private const int MaxAttempts = 5;
    private const int Batch = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebPushDeliveryWorker> _logger;

    public WebPushDeliveryWorker(IServiceScopeFactory scopeFactory, ILogger<WebPushDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "WebPushDeliveryWorker: unexpected error during batch");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
        var eventRepo  = scope.ServiceProvider.GetRequiredService<INotificationEventRepository>();
        var pushSubRepo = scope.ServiceProvider.GetRequiredService<IPushSubscriptionRepository>();
        var sender     = scope.ServiceProvider.GetRequiredService<IWebPushSender>();
        var renderer   = scope.ServiceProvider.GetRequiredService<IPushPayloadRenderer>();

        var rows = await outboxRepo.ClaimDueBatchAsync(DeliveryTarget.WebPush, Batch, DateTime.UtcNow, ct);
        _logger.LogDebug("WebPushDeliveryWorker: claimed {Count} due rows", rows.Count);

        foreach (var row in rows)
        {
            if (row.NotificationEvent is null) continue;

            var evt = row.NotificationEvent;
            var subs = await pushSubRepo.ListByUserAsync(evt.UserId, ct);
            if (subs.Count == 0)
            {
                await outboxRepo.MarkDeliveredAsync(row.Id, ct);
                continue;
            }

            var payload = renderer.Render(evt);
            bool anySuccess = false;
            bool transientError = false;

            foreach (var sub in subs)
            {
                try
                {
                    var sent = await sender.SendAsync(sub, payload, ct);
                    if (!sent)
                        await pushSubRepo.DeleteByEndpointAsync(sub.Endpoint, sub.UserId, ct);
                    else
                        anySuccess = true;
                }
                catch
                {
                    transientError = true;
                }
            }

            if (transientError && !anySuccess)
            {
                var attempt = row.AttemptCount;
                var backoff = attempt < Backoffs.Length ? Backoffs[attempt] : Backoffs[^1];
                var nextAt  = DateTime.UtcNow + backoff;

                if (row.AttemptCount + 1 >= MaxAttempts)
                {
                    await outboxRepo.MarkPermanentFailureAsync(row.Id, "Max retry attempts reached", ct);
                    _logger.LogError("WebPush permanent failure for outbox {OutboxId}", row.Id);
                }
                else
                {
                    await outboxRepo.MarkRetryAsync(row.Id, nextAt, "Transient push failure", ct);
                    _logger.LogWarning("WebPush transient failure for outbox {OutboxId} (attempt {Attempt})", row.Id, row.AttemptCount + 1);
                }
            }
            else
            {
                await outboxRepo.MarkDeliveredAsync(row.Id, ct);
                _logger.LogInformation("WebPush delivered for user {UserId} (outboxId={OutboxId})", evt.UserId, row.Id);
            }
        }
    }
}
