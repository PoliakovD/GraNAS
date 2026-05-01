using GraNAS.LogService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenSearch.Client;

namespace GraNAS.LogService.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class LogsController : ControllerBase
{
    private const string IndexPattern = "granas-logs-*";

    private readonly OpenSearchClient _os;

    public LogsController(OpenSearchClient os) => _os = os;

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

        var response = await _os.SearchAsync<LogDocument>(s => s
            .Index(IndexPattern)
            .From((page - 1) * pageSize)
            .Size(pageSize)
            .Query(q => musts.Count > 0
                ? new QueryContainer(new BoolQuery { Must = musts })
                : q.MatchAll())
            .Sort(ss => ss.Descending("@timestamp"))
        );

        if (!response.IsValid)
            return StatusCode(502, new { error = "opensearch_error", detail = response.DebugInformation });

        return Ok(new
        {
            total = response.Total,
            page,
            pageSize,
            logs = response.Documents
        });
    }
}
