using System.Text.Json.Serialization;

namespace GraNAS.Desktop.Contracts.Auth;

public class LogoutRequest
{
  [JsonPropertyName("refreshToken")]
  public string? RefreshToken { get; set; }

  [JsonPropertyName("allSessions")]
  public bool? AllSessions { get; set; }
}
