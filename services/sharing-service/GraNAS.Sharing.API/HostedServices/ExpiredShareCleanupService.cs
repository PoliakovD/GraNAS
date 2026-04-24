using System;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Sharing.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GraNAS.Sharing.API.HostedServices;

public class ExpiredShareCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredShareCleanupService> _logger;
    private readonly TimeSpan _interval;

    public ExpiredShareCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredShareCleanupService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var minutes = config.GetValue<int>("Sharing:CleanupIntervalMinutes", 60);
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var shareService = scope.ServiceProvider.GetRequiredService<IShareService>();
                var deleted = await shareService.DeleteExpiredAsync(stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Cleaned up {Count} expired share links", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during expired share link cleanup");
            }
        }
    }
}
