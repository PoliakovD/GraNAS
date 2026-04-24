using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GraNAS.LogService.Health;

public class ElasticsearchHealthCheck(ElasticsearchClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        var response = await client.PingAsync(cancellationToken: ct);
        return response.IsValidResponse
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Elasticsearch unreachable");
    }
}
