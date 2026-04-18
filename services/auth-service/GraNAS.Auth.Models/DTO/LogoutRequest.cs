namespace GraNAS.Auth.Models.DTO;

public class LogoutRequest
{
  public string? RefreshToken { get; set; }
  public bool? AllSessions { get; set; }
}
