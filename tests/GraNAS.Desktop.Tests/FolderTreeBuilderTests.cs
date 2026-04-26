using FluentAssertions;
using GraNAS.Desktop.App.Services.Folders;
using GraNAS.Desktop.Contracts.Metadata;

namespace GraNAS.Desktop.Tests;

public class FolderTreeBuilderTests
{
  private static FolderResponse Folder(Guid id, Guid owner, Guid? parent = null, string name = "F")
    => new() { Id = id, OwnerId = owner, ParentFolderId = parent, Name = name };

  [Fact]
  public void Build_EmptyList_ReturnsEmptyTree()
  {
    var result = FolderTreeBuilder.Build([], Guid.NewGuid());
    result.Should().BeEmpty();
  }

  [Fact]
  public void Build_OnlyOwnedRootFolders_ReturnsFlat()
  {
    var owner = Guid.NewGuid();
    var f1 = Folder(Guid.NewGuid(), owner, name: "A");
    var f2 = Folder(Guid.NewGuid(), owner, name: "B");

    var result = FolderTreeBuilder.Build([f1, f2], owner);

    result.Should().HaveCount(2);
    result.Should().NotContain(n => n.Children.Count > 0);
  }

  [Fact]
  public void Build_SubfolderPlacedUnderParent()
  {
    var owner = Guid.NewGuid();
    var root = Folder(Guid.NewGuid(), owner, name: "Root");
    var child = Folder(Guid.NewGuid(), owner, root.Id, "Child");

    var result = FolderTreeBuilder.Build([root, child], owner);

    result.Should().HaveCount(1);
    result[0].Folder.Id.Should().Be(root.Id);
    result[0].Children.Should().HaveCount(1);
    result[0].Children[0].Folder.Id.Should().Be(child.Id);
  }

  [Fact]
  public void Build_FiltersOutFoldersOwnedByOthers()
  {
    var owner = Guid.NewGuid();
    var other = Guid.NewGuid();
    var mine = Folder(Guid.NewGuid(), owner);
    var theirs = Folder(Guid.NewGuid(), other);

    var result = FolderTreeBuilder.Build([mine, theirs], owner);

    result.Should().HaveCount(1);
    result[0].Folder.OwnerId.Should().Be(owner);
  }

  [Fact]
  public void Build_OrphanedChild_PlacedAtRoot()
  {
    // Child references a non-existent parent → treated as root
    var owner = Guid.NewGuid();
    var child = Folder(Guid.NewGuid(), owner, Guid.NewGuid(), "Orphan");

    var result = FolderTreeBuilder.Build([child], owner);

    result.Should().HaveCount(1);
    result[0].Folder.Id.Should().Be(child.Id);
  }

  [Fact]
  public void GetSharedWithMe_ReturnsOnlyFoldersNotOwnedByCurrentUser()
  {
    var me = Guid.NewGuid();
    var other = Guid.NewGuid();
    var mine = Folder(Guid.NewGuid(), me);
    var shared = Folder(Guid.NewGuid(), other);

    var result = FolderTreeBuilder.GetSharedWithMe([mine, shared], me);

    result.Should().HaveCount(1);
    result[0].OwnerId.Should().Be(other);
  }
}
