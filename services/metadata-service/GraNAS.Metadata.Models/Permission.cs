namespace GraNAS.Metadata.Models;

public class Permission
{
  public Guid Id { get; set; }
  public Guid FolderId { get; set; }
  public Guid UserId { get; set; }
  public AccessLevel AccessLevel { get; set; }
  public string? Path { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }

  public Folder? Folder { get; set; }
}
