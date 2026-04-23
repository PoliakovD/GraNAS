namespace GraNAS.Auth.Models.DTO;

public class TokenResponse
{
  public string AccessToken { get; set; } = null!;
  public string RefreshToken { get; set; } = null!;
  public long ExpiresIn { get; set; }
  public string TokenType { get; set; } = "bearer";
}
