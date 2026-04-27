using System.Text.Json.Serialization;

namespace GraNAS.Desktop.Contracts.Auth;

public class TokenResponse
{
  [JsonPropertyName("access_token")]
  public string AccessToken { get; set; } = string.Empty;

  [JsonPropertyName("refresh_token")]
  public string RefreshToken { get; set; } = string.Empty;

  [JsonPropertyName("expires_in")]
  public long ExpiresIn { get; set; }

  [JsonPropertyName("token_type")]
  public string TokenType { get; set; } = "Bearer";
}
