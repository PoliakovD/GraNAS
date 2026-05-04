namespace GraNAS.Desktop.Contracts.Auth;

public class MeResponse
{
  public Guid Id { get; set; }
  public string Email { get; set; } = string.Empty;
  public bool IsAdmin { get; set; }
}
