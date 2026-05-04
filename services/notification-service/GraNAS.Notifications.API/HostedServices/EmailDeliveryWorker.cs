using System;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Notifications.Models;
using GraNAS.Notifications.Models.Repositories;
using GraNAS.Notifications.Services.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GraNAS.Notifications.API.HostedServices;

public class EmailDeliveryWorker : BackgroundService
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
    private const int Batch = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailDeliveryWorker> _logger;

    public EmailDeliveryWorker(IServiceScopeFactory scopeFactory, ILogger<EmailDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "EmailDeliveryWorker: unexpected error during batch");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
        var deliveryService = scope.ServiceProvider.GetRequiredService<Infrastructure.NotificationDeliveryService>();

        var rows = await outboxRepo.ClaimDueBatchAsync(DeliveryTarget.Email, Batch, DateTime.UtcNow, ct);
        _logger.LogDebug("EmailDeliveryWorker: claimed {Count} due rows", rows.Count);

        foreach (var row in rows)
        {
            if (row.NotificationEvent is null) continue;

            var result = await deliveryService.DeliverEmailAsync(row, row.NotificationEvent, ct);

            switch (result)
            {
                case DeliveryResult.Success:
                    await outboxRepo.MarkDeliveredAsync(row.Id, ct);
                    _logger.LogInformation(
                        "Email delivered to {UserId} for {EventType} (outboxId={OutboxId})",
                        row.NotificationEvent.UserId, row.NotificationEvent.Type, row.Id);
                    break;

                case DeliveryResult.TransientFailure:
                    var attemptAfter = row.AttemptCount;
                    var backoff = attemptAfter < Backoffs.Length
                        ? Backoffs[attemptAfter]
                        : Backoffs[^1];
                    var nextAttempt = DateTime.UtcNow + backoff;

                    if (row.AttemptCount + 1 >= MaxAttempts)
                    {
                        await outboxRepo.MarkPermanentFailureAsync(row.Id, "Max retry attempts reached", ct);
                        _logger.LogError(
                            "Email permanent failure for outbox {OutboxId} after {Attempt} attempts",
                            row.Id, row.AttemptCount + 1);
                    }
                    else
                    {
                        await outboxRepo.MarkRetryAsync(row.Id, nextAttempt, "Transient SMTP failure", ct);
                        _logger.LogWarning(
                            "Email transient failure for outbox {OutboxId} (attempt {Attempt}); next at {NextAttemptAt}",
                            row.Id, row.AttemptCount + 1, nextAttempt);
                    }
                    break;

                case DeliveryResult.PermanentFailure:
                    await outboxRepo.MarkPermanentFailureAsync(row.Id, "Permanent failure", ct);
                    _logger.LogError(
                        "Email permanent failure for outbox {OutboxId} after {Attempt} attempts",
                        row.Id, row.AttemptCount + 1);
                    break;
            }
        }
    }
}
