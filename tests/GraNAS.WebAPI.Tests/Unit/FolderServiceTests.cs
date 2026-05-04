using System;
using System.Collections.Generic;
using System.Linq;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Models.Repositories;
using GraNAS.Metadata.Services.Implementations;
using GraNAS.Metadata.Services.Interfaces;
using GraNAS.Shared.Messaging.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GraNAS.WebAPI.Tests.Unit;

public class FolderServiceTests
{
    private readonly Mock<IFolderRepository> _repo = new();
    private readonly Mock<IPermissionRepository> _permRepo = new();
    private readonly Mock<IPermissionService> _permSvc = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly FolderService _sut;

    public FolderServiceTests()
    {
        // Default: no shared folders, no affected users on delete
        _permRepo.Setup(r => r.ListByUserAsync(It.IsAny<Guid>()))
                 .ReturnsAsync(Array.Empty<Permission>());
        _permRepo.Setup(r => r.GetUsersForFolderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Array.Empty<Guid>());

        _sut = new FolderService(_repo.Object, _permRepo.Object, _permSvc.Object, _eventPublisher.Object, NullLogger<FolderService>.Instance);
    }

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
        _permSvc.Setup(s => s.HasAccessAsync(userId, parentId, AccessLevel.Full))
                .ReturnsAsync(true);
        _repo.Setup(r => r.CreateAsync(It.IsAny<Folder>())).Returns(Task.CompletedTask);

        var result = await _sut.CreateFolderAsync(userId,
            new CreateFolderRequest { Name = "Child", ParentFolderId = parentId });

        Assert.Equal(CreateFolderError.None, result.Error);
        Assert.Equal(parentId, result.Response!.ParentFolderId);
    }

    [Fact]
    public async Task Create_ParentNotFound_ReturnsParentNotFoundOrForbidden()
    {
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _permSvc.Setup(s => s.HasAccessAsync(userId, parentId, AccessLevel.Full))
                .ReturnsAsync(false);

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
        var parentId = Guid.NewGuid();
        _permSvc.Setup(s => s.HasAccessAsync(userId, parentId, AccessLevel.Full))
                .ReturnsAsync(false);

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

    [Fact]
    public async Task GetUserFolders_ReturnsOwnedAndSharedWithCorrectAccessLevel()
    {
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var ownedId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();

        var sharedFolder = new Folder { Id = sharedId, OwnerId = ownerId, Name = "Shared" };

        _repo.Setup(r => r.GetUserFoldersAsync(userId)).ReturnsAsync(new List<Folder>
        {
            new() { Id = ownedId, OwnerId = userId, Name = "Owned" }
        });
        _permRepo.Setup(r => r.ListByUserAsync(userId)).ReturnsAsync(new List<Permission>
        {
            new() { FolderId = sharedId, UserId = userId, AccessLevel = AccessLevel.View, Folder = sharedFolder }
        });

        var result = (await _sut.GetUserFoldersAsync(userId)).ToList();

        Assert.Equal(2, result.Count);
        var owned = result.Single(f => f.Id == ownedId);
        var shared = result.Single(f => f.Id == sharedId);
        Assert.Equal(AccessLevel.Full, owned.AccessLevel);
        Assert.Equal(userId, owned.OwnerId);
        Assert.Equal(AccessLevel.View, shared.AccessLevel);
        Assert.Equal(ownerId, shared.OwnerId);
    }
}
