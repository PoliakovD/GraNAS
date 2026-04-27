namespace GraNAS.Desktop.Contracts.Sharing;

public class CreateShareRequest
{
  public DateTime ExpiresAt { get => field.ToUniversalTime(); set; }
  public string? Path { get; set; }
}
