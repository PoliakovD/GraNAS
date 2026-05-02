using OpenSearch.Client;
using OpenSearch.Net;
using HttpMethod = OpenSearch.Net.HttpMethod;

namespace GraNAS.LogService.Infrastructure;

public sealed class IndexTemplateInitializer : IHostedService
{
    private readonly OpenSearchClient _os;
    private readonly ILogger<IndexTemplateInitializer> _logger;

    public IndexTemplateInitializer(OpenSearchClient os, ILogger<IndexTemplateInitializer> logger)
    {
        _os = os;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var assembly = typeof(IndexTemplateInitializer).Assembly;
            using var stream = assembly.GetManifestResourceStream(
                "GraNAS.LogService.Resources.granas-logs-template.json");

            if (stream is null)
            {
                _logger.LogWarning("Index template resource not found — skipping template registration");
                return;
            }

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync(cancellationToken);

            var response = await _os.LowLevel.DoRequestAsync<StringResponse>(
                HttpMethod.PUT,
                "/_index_template/granas-logs-template",
                cancellationToken,
                PostData.String(json));

            if (response.Success)
                _logger.LogInformation("OpenSearch index template 'granas-logs-template' registered");
            else
                _logger.LogWarning("Failed to register index template: {Detail}", response.DebugInformation);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Index template registration failed — logs will use dynamic mapping");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
