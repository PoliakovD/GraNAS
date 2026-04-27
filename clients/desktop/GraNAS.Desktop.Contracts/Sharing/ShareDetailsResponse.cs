namespace GraNAS.Desktop.Contracts.Sharing;

public class ShareDetailsResponse
{
  public Guid FolderId { get; set; }
  public string FolderName { get; set; } = string.Empty;
  public Guid OwnerId { get; set; }
  public string? Path { get; set; }
  public DateTime ExpiresAt { get; set; }
}
