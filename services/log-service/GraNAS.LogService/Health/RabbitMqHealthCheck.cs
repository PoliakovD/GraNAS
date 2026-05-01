using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace GraNAS.LogService.Health;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;

    public RabbitMqHealthCheck(IConfiguration config) => _config = config;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _config["RabbitMQ:Host"] ?? "localhost",
                UserName = _config["RabbitMQ:Username"] ?? "guest",
                Password = _config["RabbitMQ:Password"] ?? "guest"
            };
            await using var conn = await factory.CreateConnectionAsync(cancellationToken);
            return conn.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ connection is not open");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ unreachable", ex);
        }
    }
}
