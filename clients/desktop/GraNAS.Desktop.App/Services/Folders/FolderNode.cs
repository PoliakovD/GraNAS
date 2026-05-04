using GraNAS.Desktop.Contracts.Metadata;

namespace GraNAS.Desktop.App.Services.Folders;

public class FolderNode
{
  public FolderResponse Folder { get; }
  public List<FolderNode> Children { get; } = [];

  public FolderNode(FolderResponse folder) => Folder = folder;
}
