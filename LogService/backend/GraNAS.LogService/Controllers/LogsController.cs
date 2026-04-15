using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GraNAS.LogService.Data;
using GraNAS.LogService.Models;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
  private readonly LogDbContext _db;
  public LogsController(LogDbContext db) => _db = db;

  [HttpGet]
  public async Task<IActionResult> GetLogs(
    [FromQuery] string? service,
    [FromQuery] string? level,
    [FromQuery] string? correlationId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50)
  {
    var query = _db.Logs.AsQueryable();
    if (!string.IsNullOrEmpty(service)) query = query.Where(l => l.Service == service);
    if (!string.IsNullOrEmpty(level)) query = query.Where(l => l.Level == level);
    if (!string.IsNullOrEmpty(correlationId)) query = query.Where(l => l.CorrelationId == correlationId);

    var total = await query.CountAsync();
    var logs = await query
      .OrderByDescending(l => l.Timestamp)
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .ToListAsync();

    return Ok(new { total, page, pageSize, logs });
  }
}
