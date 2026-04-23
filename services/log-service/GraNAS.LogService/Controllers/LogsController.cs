using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using GraNAS.LogService.Models;
using Microsoft.AspNetCore.Mvc;

namespace GraNAS.LogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
  private const string IndexPattern = "granas-logs-*";

  private readonly ElasticsearchClient _es;

  public LogsController(ElasticsearchClient es) => _es = es;

  /// <summary>
  /// Поиск логов с фильтрацией и пагинацией
  /// </summary>
  [HttpGet]
  public async Task<IActionResult> GetLogs(
    [FromQuery] string? service,
    [FromQuery] string? level,
    [FromQuery] string? correlationId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50)
  {
    var musts = new List<Query>();

    if (!string.IsNullOrEmpty(service))
      musts.Add(new TermQuery(new Field("Application")) { Value = service });

    if (!string.IsNullOrEmpty(level))
      musts.Add(new TermQuery(new Field("level")) { Value = level.ToLowerInvariant() });

    if (!string.IsNullOrEmpty(correlationId))
      musts.Add(new TermQuery(new Field("CorrelationId")) { Value = correlationId });

    var query = musts.Count > 0
      ? Query.Bool(new BoolQuery { Must = musts })
      : Query.MatchAll(new MatchAllQuery());

    var response = await _es.SearchAsync<LogDocument>(s => s
      .Indices(IndexPattern)
      .From((page - 1) * pageSize)
      .Size(pageSize)
      .Query(query)
      .Sort(sort => sort.Field(
        new Field("@timestamp"),
        new FieldSort { Order = SortOrder.Desc }))
    );

    if (!response.IsValidResponse)
      return StatusCode(502, new { error = "elasticsearch_error", detail = response.DebugInformation });

    return Ok(new
    {
      total = response.Total,
      page,
      pageSize,
      logs = response.Documents
    });
  }
}
