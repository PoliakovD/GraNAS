namespace GraNAS.Desktop.Contracts.Metadata;

public class FolderResponse
{
  public Guid Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public Guid? ParentFolderId { get; set; }
  public Guid OwnerId { get; set; }
  public string? OwnerEmail { get; set; }
  public AccessLevel AccessLevel { get; set; }
  public string? Path { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public DateTime? LastAccessedAt { get; set; }
}
