namespace GraNAS.Desktop.Contracts.Sharing;

public class ShareLinkOwnerResponse
{
  public Guid Id { get; set; }
  public Guid FolderId { get; set; }
  public string FolderName { get; set; } = string.Empty;
  public string? Path { get; set; }
  public string? ShareUrl { get; set; }
  public DateTime? ExpiresAt { get; set; }
  public bool Revoked { get; set; }
  public DateTime CreatedAt { get; set; }
  public int? OpenCount { get; set; }
}
