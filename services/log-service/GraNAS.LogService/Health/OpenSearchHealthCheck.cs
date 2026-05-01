using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenSearch.Client;

namespace GraNAS.LogService.Health;

public class OpenSearchHealthCheck(OpenSearchClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        var response = await client.PingAsync();
        return response.IsValid
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("OpenSearch unreachable");
    }
}
