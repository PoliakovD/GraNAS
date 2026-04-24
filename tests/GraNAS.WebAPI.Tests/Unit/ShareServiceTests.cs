using GraNAS.Sharing.Models;
using GraNAS.Sharing.Models.DTO;
using GraNAS.Sharing.Models.Repositories;
using GraNAS.Sharing.Services.Implementations;
using GraNAS.Sharing.Services.Interfaces;
using Moq;

namespace GraNAS.WebAPI.Tests.Unit;

public class ShareServiceTests
{
    private readonly Mock<IShareLinkRepository> _repo = new();
    private readonly Mock<ITokenGenerator> _tokenGen = new();
    private readonly Mock<IMetadataServiceClient> _metadataClient = new();
    private readonly Mock<IShareEventPublisher> _eventPublisher = new();
    private readonly ShareService _sut;

    public ShareServiceTests()
    {
        _tokenGen.Setup(t => t.GenerateToken()).Returns("test_token_value");
        _tokenGen.Setup(t => t.ComputeHash(It.IsAny<string>())).Returns("testhash64charslong000000000000000000000000000000000000000000");
        _sut = new ShareService(_repo.Object, _tokenGen.Object, _metadataClient.Object, _eventPublisher.Object);
    }

    // ──────────────── CreateAsync ────────────────

    [Fact]
    public async Task CreateAsync_OwnerExists_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _metadataClient
            .Setup(c => c.GetFolderForOwnerAsync(folderId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderInfo(folderId, "Test", ownerId));
        _repo.Setup(r => r.CreateAsync(It.IsAny<ShareLink>())).Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(ownerId, folderId, new CreateShareRequest
        {
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(CreateShareError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal("test_token_value", result.Response!.Token);
    }

    [Fact]
    public async Task CreateAsync_FolderNotOwnedByUser_ReturnsFolderNotFoundOrForbidden()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _metadataClient
            .Setup(c => c.GetFolderForOwnerAsync(folderId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FolderInfo?)null);

        var result = await _sut.CreateAsync(ownerId, folderId, new CreateShareRequest
        {
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(CreateShareError.FolderNotFoundOrForbidden, result.Error);
        _repo.Verify(r => r.CreateAsync(It.IsAny<ShareLink>()), Times.Never);
    }

    // ──────────────── GetByTokenAsync ────────────────

    [Fact]
    public async Task GetByTokenAsync_ValidToken_ReturnsFolderDetails()
    {
        var folderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _tokenGen.Setup(t => t.ComputeHash("valid_token")).Returns("hash");
        _repo.Setup(r => r.GetByTokenHashAsync("hash")).ReturnsAsync(new ShareLink
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            OwnerId = ownerId,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Revoked = false,
            CreatedAt = DateTime.UtcNow
        });
        _metadataClient
            .Setup(c => c.GetFolderAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderInfo(folderId, "FolderName", ownerId));

        var result = await _sut.GetByTokenAsync("valid_token");

        Assert.NotNull(result);
        Assert.Equal(folderId, result!.FolderId);
        Assert.Equal("FolderName", result.FolderName);
    }

    [Fact]
    public async Task GetByTokenAsync_ExpiredToken_ReturnsNull()
    {
        _tokenGen.Setup(t => t.ComputeHash("expired_token")).Returns("expiredhash");
        _repo.Setup(r => r.GetByTokenHashAsync("expiredhash")).ReturnsAsync(new ShareLink
        {
            Id = Guid.NewGuid(),
            FolderId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            TokenHash = "expiredhash",
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            Revoked = false,
            CreatedAt = DateTime.UtcNow
        });

        var result = await _sut.GetByTokenAsync("expired_token");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByTokenAsync_UnknownToken_ReturnsNull()
    {
        _tokenGen.Setup(t => t.ComputeHash("ghost")).Returns("ghosthash");
        _repo.Setup(r => r.GetByTokenHashAsync("ghosthash")).ReturnsAsync((ShareLink?)null);

        var result = await _sut.GetByTokenAsync("ghost");
        Assert.Null(result);
    }

    // ──────────────── IsRevokedAsync ────────────────

    [Fact]
    public async Task IsRevokedAsync_RevokedLink_ReturnsTrue()
    {
        _tokenGen.Setup(t => t.ComputeHash("rev_token")).Returns("revhash");
        _repo.Setup(r => r.GetByTokenHashAsync("revhash")).ReturnsAsync(new ShareLink
        {
            Id = Guid.NewGuid(), FolderId = Guid.NewGuid(), OwnerId = Guid.NewGuid(),
            TokenHash = "revhash", ExpiresAt = DateTime.UtcNow.AddDays(1),
            Revoked = true, CreatedAt = DateTime.UtcNow
        });

        var result = await _sut.IsRevokedAsync("rev_token");
        Assert.True(result);
    }

    [Fact]
    public async Task IsRevokedAsync_ActiveLink_ReturnsFalse()
    {
        _tokenGen.Setup(t => t.ComputeHash("active_token")).Returns("activehash");
        _repo.Setup(r => r.GetByTokenHashAsync("activehash")).ReturnsAsync(new ShareLink
        {
            Id = Guid.NewGuid(), FolderId = Guid.NewGuid(), OwnerId = Guid.NewGuid(),
            TokenHash = "activehash", ExpiresAt = DateTime.UtcNow.AddDays(1),
            Revoked = false, CreatedAt = DateTime.UtcNow
        });

        var result = await _sut.IsRevokedAsync("active_token");
        Assert.False(result);
    }

    // ──────────────── RevokeByTokenAsync ────────────────

    [Fact]
    public async Task RevokeByTokenAsync_OwnerRevokes_ReturnsNoneAndPublishesEvent()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        _tokenGen.Setup(t => t.ComputeHash("revoke_token")).Returns("revokehash");
        _repo.Setup(r => r.GetByTokenHashAsync("revokehash")).ReturnsAsync(new ShareLink
        {
            Id = linkId, FolderId = folderId, OwnerId = ownerId,
            TokenHash = "revokehash", ExpiresAt = DateTime.UtcNow.AddDays(1),
            Revoked = false, CreatedAt = DateTime.UtcNow
        });
        _repo.Setup(r => r.UpdateAsync(It.IsAny<ShareLink>())).Returns(Task.CompletedTask);
        _eventPublisher.Setup(p => p.PublishShareRevokedAsync(linkId, folderId, ownerId)).Returns(Task.CompletedTask);

        var result = await _sut.RevokeByTokenAsync(ownerId, "revoke_token");

        Assert.Equal(RevokeShareError.None, result.Error);
        _eventPublisher.Verify(p => p.PublishShareRevokedAsync(linkId, folderId, ownerId), Times.Once);
    }

    [Fact]
    public async Task RevokeByTokenAsync_NonOwner_ReturnsNotFoundOrForbidden()
    {
        var realOwnerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        _tokenGen.Setup(t => t.ComputeHash("attack_token")).Returns("attackhash");
        _repo.Setup(r => r.GetByTokenHashAsync("attackhash")).ReturnsAsync(new ShareLink
        {
            Id = Guid.NewGuid(), FolderId = Guid.NewGuid(), OwnerId = realOwnerId,
            TokenHash = "attackhash", ExpiresAt = DateTime.UtcNow.AddDays(1),
            Revoked = false, CreatedAt = DateTime.UtcNow
        });

        var result = await _sut.RevokeByTokenAsync(attackerId, "attack_token");

        Assert.Equal(RevokeShareError.NotFoundOrForbidden, result.Error);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<ShareLink>()), Times.Never);
    }

    // ──────────────── RevokeByIdAsync ────────────────

    [Fact]
    public async Task RevokeByIdAsync_OwnerRevokes_ReturnsNone()
    {
        var ownerId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdForOwnerAsync(linkId, ownerId)).ReturnsAsync(new ShareLink
        {
            Id = linkId, FolderId = folderId, OwnerId = ownerId,
            TokenHash = "somehash", ExpiresAt = DateTime.UtcNow.AddDays(1),
            Revoked = false, CreatedAt = DateTime.UtcNow
        });
        _repo.Setup(r => r.UpdateAsync(It.IsAny<ShareLink>())).Returns(Task.CompletedTask);
        _eventPublisher.Setup(p => p.PublishShareRevokedAsync(linkId, folderId, ownerId)).Returns(Task.CompletedTask);

        var result = await _sut.RevokeByIdAsync(ownerId, linkId);

        Assert.Equal(RevokeShareError.None, result.Error);
    }

    [Fact]
    public async Task RevokeByIdAsync_NotFound_ReturnsNotFoundOrForbidden()
    {
        var ownerId = Guid.NewGuid();
        var randomId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdForOwnerAsync(randomId, ownerId)).ReturnsAsync((ShareLink?)null);

        var result = await _sut.RevokeByIdAsync(ownerId, randomId);

        Assert.Equal(RevokeShareError.NotFoundOrForbidden, result.Error);
    }

    // ──────────────── DeleteExpiredAsync ────────────────

    [Fact]
    public async Task DeleteExpiredAsync_ReturnsCountFromRepo()
    {
        _repo.Setup(r => r.DeleteExpiredAsync(It.IsAny<DateTime>())).ReturnsAsync(5);

        var count = await _sut.DeleteExpiredAsync();

        Assert.Equal(5, count);
    }
}
