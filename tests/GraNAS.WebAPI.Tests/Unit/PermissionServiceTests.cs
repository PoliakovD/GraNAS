using System;
using System.Threading;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Models.Repositories;
using GraNAS.Metadata.Services.Implementations;
using GraNAS.Metadata.Services.Interfaces;
using GraNAS.Shared.Messaging.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GraNAS.WebAPI.Tests.Unit;

public class PermissionServiceTests
{
    private readonly Mock<IFolderRepository> _folders = new();
    private readonly Mock<IPermissionRepository> _permissions = new();
    private readonly Mock<IAuthServiceClient> _authClient = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly PermissionService _sut;

    public PermissionServiceTests()
    {
        _sut = new PermissionService(_folders.Object, _permissions.Object, _authClient.Object, _eventPublisher.Object, NullLogger<PermissionService>.Instance);
    }

    // ──────────────── GrantAsync ────────────────

    [Fact]
    public async Task Grant_HappyPath_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var folder = new Folder { Id = folderId, OwnerId = ownerId, Name = "F" };

        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, ownerId)).ReturnsAsync(folder);
        _authClient.Setup(c => c.GetUserByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UserInfo(targetId, "user@test.com"));
        _permissions.Setup(r => r.UpsertAsync(It.IsAny<Permission>())).Returns(Task.CompletedTask);

        var result = await _sut.GrantAsync(ownerId, folderId,
            new GrantPermissionRequest { Email = "user@test.com", AccessLevel = AccessLevel.View });

        Assert.Equal(GrantPermissionError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal(targetId, result.Response!.UserId);
        Assert.Equal(AccessLevel.View, result.Response.AccessLevel);
        _permissions.Verify(r => r.UpsertAsync(It.Is<Permission>(p =>
            p.FolderId == folderId && p.UserId == targetId && p.AccessLevel == AccessLevel.View)), Times.Once);
    }

    [Fact]
    public async Task Grant_FolderNotOwned_ReturnsFolderNotFoundOrForbidden()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, ownerId)).ReturnsAsync((Folder?)null);

        var result = await _sut.GrantAsync(ownerId, folderId,
            new GrantPermissionRequest { Email = "x@x.com", AccessLevel = AccessLevel.View });

        Assert.Equal(GrantPermissionError.FolderNotFoundOrForbidden, result.Error);
        _authClient.Verify(c => c.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _permissions.Verify(r => r.UpsertAsync(It.IsAny<Permission>()), Times.Never);
    }

    [Fact]
    public async Task Grant_UserNotFound_ReturnsUserNotFound()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, ownerId))
                .ReturnsAsync(new Folder { Id = folderId, OwnerId = ownerId, Name = "F" });
        _authClient.Setup(c => c.GetUserByEmailAsync("ghost@test.com", It.IsAny<CancellationToken>()))
                   .ReturnsAsync((UserInfo?)null);

        var result = await _sut.GrantAsync(ownerId, folderId,
            new GrantPermissionRequest { Email = "ghost@test.com", AccessLevel = AccessLevel.View });

        Assert.Equal(GrantPermissionError.UserNotFound, result.Error);
        _permissions.Verify(r => r.UpsertAsync(It.IsAny<Permission>()), Times.Never);
    }

    [Fact]
    public async Task Grant_SelfGrant_ReturnsSuccessWithoutUpsert()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, ownerId))
                .ReturnsAsync(new Folder { Id = folderId, OwnerId = ownerId, Name = "F" });
        _authClient.Setup(c => c.GetUserByEmailAsync("owner@test.com", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UserInfo(ownerId, "owner@test.com")); // same as ownerId

        var result = await _sut.GrantAsync(ownerId, folderId,
            new GrantPermissionRequest { Email = "owner@test.com", AccessLevel = AccessLevel.View });

        Assert.Equal(GrantPermissionError.None, result.Error);
        _permissions.Verify(r => r.UpsertAsync(It.IsAny<Permission>()), Times.Never);
    }

    [Fact]
    public async Task Grant_FullAccessWithPath_StoresPath()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, ownerId))
                .ReturnsAsync(new Folder { Id = folderId, OwnerId = ownerId, Name = "F" });
        _authClient.Setup(c => c.GetUserByEmailAsync("u@t.com", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UserInfo(targetId, "u@t.com"));
        _permissions.Setup(r => r.UpsertAsync(It.IsAny<Permission>())).Returns(Task.CompletedTask);

        var result = await _sut.GrantAsync(ownerId, folderId,
            new GrantPermissionRequest { Email = "u@t.com", AccessLevel = AccessLevel.Full, Path = "subdir/file.txt" });

        Assert.Equal(GrantPermissionError.None, result.Error);
        Assert.Equal("subdir/file.txt", result.Response!.Path);
        _permissions.Verify(r => r.UpsertAsync(It.Is<Permission>(p => p.Path == "subdir/file.txt")), Times.Once);
    }

    // ──────────────── RevokeAsync ────────────────

    [Fact]
    public async Task Revoke_HappyPath_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, ownerId))
                .ReturnsAsync(new Folder { Id = folderId, OwnerId = ownerId, Name = "F" });
        _permissions.Setup(r => r.DeleteAsync(folderId, targetId)).ReturnsAsync(true);

        var result = await _sut.RevokeAsync(ownerId, folderId, targetId);

        Assert.Equal(RevokePermissionError.None, result.Error);
    }

    [Fact]
    public async Task Revoke_FolderNotOwned_ReturnsFolderNotFoundOrForbidden()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, ownerId)).ReturnsAsync((Folder?)null);

        var result = await _sut.RevokeAsync(ownerId, folderId, Guid.NewGuid());

        Assert.Equal(RevokePermissionError.FolderNotFoundOrForbidden, result.Error);
    }

    [Fact]
    public async Task Revoke_PermissionNotFound_ReturnsPermissionNotFound()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, ownerId))
                .ReturnsAsync(new Folder { Id = folderId, OwnerId = ownerId, Name = "F" });
        _permissions.Setup(r => r.DeleteAsync(folderId, targetId)).ReturnsAsync(false);

        var result = await _sut.RevokeAsync(ownerId, folderId, targetId);

        Assert.Equal(RevokePermissionError.PermissionNotFound, result.Error);
    }

    // ──────────────── HasAccessAsync ────────────────

    [Fact]
    public async Task HasAccess_Owner_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, userId))
                .ReturnsAsync(new Folder { Id = folderId, OwnerId = userId });

        Assert.True(await _sut.HasAccessAsync(userId, folderId, AccessLevel.Full));
        _permissions.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HasAccess_ViewPermission_RequiredView_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, userId)).ReturnsAsync((Folder?)null);
        _permissions.Setup(r => r.GetAsync(folderId, userId))
                    .ReturnsAsync(new Permission { AccessLevel = AccessLevel.View });

        Assert.True(await _sut.HasAccessAsync(userId, folderId, AccessLevel.View));
    }

    [Fact]
    public async Task HasAccess_ViewPermission_RequiredFull_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, userId)).ReturnsAsync((Folder?)null);
        _permissions.Setup(r => r.GetAsync(folderId, userId))
                    .ReturnsAsync(new Permission { AccessLevel = AccessLevel.View });

        Assert.False(await _sut.HasAccessAsync(userId, folderId, AccessLevel.Full));
    }

    [Fact]
    public async Task HasAccess_FullPermission_RequiredView_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, userId)).ReturnsAsync((Folder?)null);
        _permissions.Setup(r => r.GetAsync(folderId, userId))
                    .ReturnsAsync(new Permission { AccessLevel = AccessLevel.Full });

        Assert.True(await _sut.HasAccessAsync(userId, folderId, AccessLevel.View));
    }

    [Fact]
    public async Task HasAccess_NoPermission_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _folders.Setup(r => r.GetByIdForOwnerAsync(folderId, userId)).ReturnsAsync((Folder?)null);
        _permissions.Setup(r => r.GetAsync(folderId, userId)).ReturnsAsync((Permission?)null);

        Assert.False(await _sut.HasAccessAsync(userId, folderId, AccessLevel.View));
    }
}
