namespace GraNAS.Desktop.Contracts.Metadata;

public class CreateFolderRequest
{
  public string Name { get; set; } = string.Empty;
  public Guid? ParentFolderId { get; set; }
}
