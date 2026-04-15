using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GraNAS.LogService.Models;

[Table("logs")]
public class LogEntry
{
  [Key]
  public long Id { get; set; }
  public DateTime Timestamp { get; set; }
  public string? CorrelationId { get; set; }
  public string Service { get; set; } = string.Empty;
  public string Level { get; set; } = "Information";
  public string Message { get; set; } = string.Empty;
  public string? UserId { get; set; }
  public string? AdditionalData { get; set; } // JSON
}
