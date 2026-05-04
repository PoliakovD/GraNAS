using System.Text.Json.Serialization;

namespace GraNAS.Desktop.Contracts.Common;

public class ErrorResponse
{
  public string Error { get; set; } = string.Empty;

  [JsonPropertyName("error_description")]
  public string? ErrorDescription { get; set; }
}
