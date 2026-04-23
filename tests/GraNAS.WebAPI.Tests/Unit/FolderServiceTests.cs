using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Models.Repositories;
using GraNAS.Metadata.Services.Implementations;
using GraNAS.Metadata.Services.Interfaces;
using Moq;

namespace GraNAS.WebAPI.Tests.Unit;

public class FolderServiceTests
{
    private readonly Mock<IFolderRepository> _repo = new();
    private readonly FolderService _sut;

    public FolderServiceTests() => _sut = new FolderService(_repo.Object);

    // ──────────────── CreateFolderAsync ────────────────

    [Fact]
    public async Task Create_NoParent_CallsRepoWithNullParentAndReturnsResponse()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.CreateAsync(It.IsAny<Folder>())).Returns(Task.CompletedTask);

        var result = await _sut.CreateFolderAsync(userId, new CreateFolderRequest { Name = "Root" });

        Assert.Equal(CreateFolderError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal("Root", result.Response!.Name);
        Assert.Null(result.Response.ParentFolderId);
        _repo.Verify(r => r.CreateAsync(It.Is<Folder>(f =>
            f.ParentFolderId == null && f.OwnerId == userId && f.Name == "Root")), Times.Once);
    }

    [Fact]
    public async Task Create_ValidParent_ReturnsResponseWithParentFolderId()
    {
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdForOwnerAsync(parentId, userId))
             .ReturnsAsync(new Folder { Id = parentId, OwnerId = userId, Name = "Parent" });
        _repo.Setup(r => r.CreateAsync(It.IsAny<Folder>())).Returns(Task.CompletedTask);

        var result = await _sut.CreateFolderAsync(userId,
            new CreateFolderRequest { Name = "Child", ParentFolderId = parentId });

        Assert.Equal(CreateFolderError.None, result.Error);
        Assert.Equal(parentId, result.Response!.ParentFolderId);
        _repo.Verify(r => r.GetByIdForOwnerAsync(parentId, userId), Times.Once);
    }

    [Fact]
    public async Task Create_ParentNotFound_ReturnsParentNotFoundOrForbidden()
    {
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdForOwnerAsync(parentId, userId)).ReturnsAsync((Folder?)null);

        var result = await _sut.CreateFolderAsync(userId,
            new CreateFolderRequest { Name = "Child", ParentFolderId = parentId });

        Assert.Equal(CreateFolderError.ParentNotFoundOrForbidden, result.Error);
        Assert.Null(result.Response);
        _repo.Verify(r => r.CreateAsync(It.IsAny<Folder>()), Times.Never);
    }

    [Fact]
    public async Task Create_ParentOwnedByOtherUser_ReturnsParentNotFoundOrForbidden()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        // repo returns null because owner filter doesn't match
        _repo.Setup(r => r.GetByIdForOwnerAsync(parentId, userId)).ReturnsAsync((Folder?)null);

        var result = await _sut.CreateFolderAsync(userId,
            new CreateFolderRequest { Name = "Child", ParentFolderId = parentId });

        Assert.Equal(CreateFolderError.ParentNotFoundOrForbidden, result.Error);
    }

    // ──────────────── DeleteFolderAsync ────────────────

    [Fact]
    public async Task Delete_OwnFolder_ReturnsNone()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(folderId))
             .ReturnsAsync(new Folder { Id = folderId, OwnerId = userId });
        _repo.Setup(r => r.DeleteAsync(folderId)).Returns(Task.CompletedTask);

        var result = await _sut.DeleteFolderAsync(userId, folderId);

        Assert.Equal(DeleteFolderError.None, result.Error);
        _repo.Verify(r => r.DeleteAsync(folderId), Times.Once);
    }

    [Fact]
    public async Task Delete_FolderNotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(folderId)).ReturnsAsync((Folder?)null);

        var result = await _sut.DeleteFolderAsync(userId, folderId);

        Assert.Equal(DeleteFolderError.NotFound, result.Error);
        _repo.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Delete_FolderOwnedByOtherUser_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(folderId))
             .ReturnsAsync(new Folder { Id = folderId, OwnerId = otherId });

        var result = await _sut.DeleteFolderAsync(userId, folderId);

        Assert.Equal(DeleteFolderError.Forbidden, result.Error);
        _repo.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ──────────────── GetUserFoldersAsync ────────────────

    [Fact]
    public async Task GetUserFolders_ReturnsFlatListWithParentFolderIdMapped()
    {
        var userId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _repo.Setup(r => r.GetUserFoldersAsync(userId)).ReturnsAsync(new List<Folder>
        {
            new() { Id = rootId,  OwnerId = userId, Name = "Root",  ParentFolderId = null },
            new() { Id = childId, OwnerId = userId, Name = "Child", ParentFolderId = rootId }
        });

        var result = (await _sut.GetUserFoldersAsync(userId)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Null(result.First(f => f.Name == "Root").ParentFolderId);
        Assert.Equal(rootId, result.First(f => f.Name == "Child").ParentFolderId);
    }
}
