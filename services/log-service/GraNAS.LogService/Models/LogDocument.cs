using System.Text.Json.Serialization;

namespace GraNAS.LogService.Models;

/// <summary>
/// Документ лога, как он хранится в Elasticsearch.
/// Поля соответствуют тому, что пишет Serilog.Sinks.Elasticsearch:
/// - @timestamp, level, message — стандартные поля Serilog
/// - Application, CorrelationId, UserId, AdditionalData — из LogContext.PushProperty / Enrich.WithProperty
/// </summary>
public class LogDocument
{
  [JsonPropertyName("@timestamp")]
  public DateTime Timestamp { get; set; }

  [JsonPropertyName("level")]
  public string Level { get; set; } = string.Empty;

  [JsonPropertyName("message")]
  public string Message { get; set; } = string.Empty;

  [JsonPropertyName("Application")]
  public string? Application { get; set; }

  [JsonPropertyName("CorrelationId")]
  public string? CorrelationId { get; set; }

  [JsonPropertyName("UserId")]
  public string? UserId { get; set; }

  [JsonPropertyName("AdditionalData")]
  public string? AdditionalData { get; set; }
}
