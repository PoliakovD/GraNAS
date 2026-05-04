namespace GraNAS.Desktop.Contracts.Metadata;

public class GrantPermissionRequest
{
  public string Email { get; set; } = string.Empty;
  public AccessLevel AccessLevel { get; set; }
  public string? Path { get; set; }
}
