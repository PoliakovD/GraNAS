using System;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Notifications.Models;
using GraNAS.Notifications.Models.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GraNAS.Notifications.API.HostedServices;

public class SignalRDeliveryWorker : BackgroundService
{
    private const int Batch = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SignalRDeliveryWorker> _logger;

    public SignalRDeliveryWorker(IServiceScopeFactory scopeFactory, ILogger<SignalRDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "SignalRDeliveryWorker: unexpected error during batch");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
        var deliveryService = scope.ServiceProvider.GetRequiredService<Infrastructure.NotificationDeliveryService>();

        var rows = await outboxRepo.ClaimDueBatchAsync(DeliveryTarget.SignalR, Batch, DateTime.UtcNow, ct);
        if (rows.Count > 0)
            _logger.LogDebug("SignalRDeliveryWorker: pushed {Count} notifications", rows.Count);

        foreach (var row in rows)
        {
            if (row.NotificationEvent is null) continue;

            await deliveryService.DeliverSignalRAsync(row, row.NotificationEvent, ct);
            await outboxRepo.MarkDeliveredAsync(row.Id, ct);
        }
    }
}
