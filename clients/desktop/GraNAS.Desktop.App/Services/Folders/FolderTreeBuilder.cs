using GraNAS.Desktop.Contracts.Metadata;

namespace GraNAS.Desktop.App.Services.Folders;

public static class FolderTreeBuilder
{
  /// <summary>
  /// Builds a tree from a flat folder list for <paramref name="ownerId"/>.
  /// Port of clients/web/src/lib/buildFolderTree.ts.
  /// </summary>
  public static IReadOnlyList<FolderNode> Build(
    IEnumerable<FolderResponse> folders,
    Guid ownerId)
  {
    var owned = folders
      .Where(f => f.OwnerId == ownerId)
      .ToList();

    var nodeMap = owned.ToDictionary(f => f.Id, f => new FolderNode(f));
    var roots = new List<FolderNode>();

    foreach (var node in nodeMap.Values)
    {
      var pid = node.Folder.ParentFolderId;
      if (pid is null || !nodeMap.TryGetValue(pid.Value, out var parent))
        roots.Add(node);
      else
        parent.Children.Add(node);
    }

    return roots;
  }

  public static IReadOnlyList<FolderResponse> GetSharedWithMe(
    IEnumerable<FolderResponse> folders,
    Guid ownerId)
    => folders.Where(f => f.OwnerId != ownerId).ToList();
}
