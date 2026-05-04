using System.Text.Json.Serialization;

namespace GraNAS.Desktop.Contracts.Auth;

public class RefreshRequest
{
  [JsonPropertyName("refreshToken")]
  public string RefreshToken { get; set; } = string.Empty;
}
