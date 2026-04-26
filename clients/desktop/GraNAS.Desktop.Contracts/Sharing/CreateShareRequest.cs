namespace GraNAS.Desktop.Contracts.Sharing;

public class CreateShareRequest
{
  public DateTime ExpiresAt { get; set; }
  public string? Path { get; set; }
}
