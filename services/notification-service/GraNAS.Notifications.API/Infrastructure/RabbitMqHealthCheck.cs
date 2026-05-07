using System;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Shared.Messaging.RabbitMq;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace GraNAS.Notifications.API.Infrastructure;

public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqOptions _opts;

    public RabbitMqHealthCheck(IOptions<RabbitMqOptions> opts)
    {
        _opts = opts.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _opts.Host,
                Port = _opts.Port,
                UserName = _opts.Username,
                Password = _opts.Password
            };
            await using var connection = await factory.CreateConnectionAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
