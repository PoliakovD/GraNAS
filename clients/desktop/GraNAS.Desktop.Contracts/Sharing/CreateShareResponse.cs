namespace GraNAS.Desktop.Contracts.Sharing;

public class CreateShareResponse
{
  public Guid Id { get; set; }
  public Guid FolderId { get; set; }
  public string Token { get; set; } = string.Empty;
  public string? Path { get; set; }
  public DateTime ExpiresAt { get; set; }
  public DateTime CreatedAt { get; set; }
}
