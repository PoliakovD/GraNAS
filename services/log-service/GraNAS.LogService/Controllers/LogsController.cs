using GraNAS.LogService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;

namespace GraNAS.LogService.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class LogsController : ControllerBase
{
    private const string IndexPattern = "granas-logs-*";

    private readonly OpenSearchClient _os;
    private readonly ILogger<LogsController> _logger;

    public LogsController(OpenSearchClient os, ILogger<LogsController> logger)
    {
        _os = os;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? service,
        [FromQuery] string? level,
        [FromQuery] string? correlationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var musts = new List<QueryContainer>();

        if (!string.IsNullOrEmpty(service))
            musts.Add(new TermQuery { Field = "Service", Value = service });

        if (!string.IsNullOrEmpty(level))
            musts.Add(new TermQuery { Field = "Level", Value = level });

        if (!string.IsNullOrEmpty(correlationId))
            musts.Add(new TermQuery { Field = "CorrelationId", Value = correlationId });

        ISearchResponse<LogDocument> response;
        try
        {
            response = await _os.SearchAsync<LogDocument>(s => s
                .Index(IndexPattern)
                .From((page - 1) * pageSize)
                .Size(pageSize)
                .Query(q => musts.Count > 0
                    ? new QueryContainer(new BoolQuery { Must = musts })
                    : q.MatchAll())
                .Sort(ss => ss.Descending("@timestamp"))
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LogsController: OpenSearch query failed (service={Service} level={Level})", service, level);
            return StatusCode(502, new { error = "opensearch_error", detail = ex.Message });
        }

        if (!response.IsValid)
        {
            _logger.LogError("LogsController: OpenSearch returned invalid response (service={Service} level={Level}): {Detail}",
                service, level, response.DebugInformation);
            return StatusCode(502, new { error = "opensearch_error", detail = response.DebugInformation });
        }

        _logger.LogDebug("LogsController: returned {Count} entries (service={Service} level={Level} page={Page})",
            response.Documents.Count, service, level, page);

        return Ok(new
        {
            total = response.Total,
            page,
            pageSize,
            logs = response.Documents
        });
    }
}
