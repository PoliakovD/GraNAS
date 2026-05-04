using System.Text.Json.Serialization;

namespace GraNAS.LogService.Models;

public class LogDocument
{
    [JsonPropertyName("@timestamp")]    public DateTime Timestamp { get; set; }
    [JsonPropertyName("Level")]         public string Level { get; set; } = "";
    [JsonPropertyName("Service")]       public string? Service { get; set; }
    [JsonPropertyName("Message")]       public string Message { get; set; } = "";
    [JsonPropertyName("SourceContext")] public string? SourceContext { get; set; }
    [JsonPropertyName("ActionName")]    public string? ActionName { get; set; }
    [JsonPropertyName("Method")]        public string? Method { get; set; }
    [JsonPropertyName("Parameters")]    public Dictionary<string, object?>? Parameters { get; set; }
    [JsonPropertyName("CorrelationId")] public string? CorrelationId { get; set; }
    [JsonPropertyName("Exception")]     public string? Exception { get; set; }
    [JsonPropertyName("Environment")]   public string? Environment { get; set; }
    [JsonPropertyName("UserId")]        public string? UserId { get; set; }
}
