using System.Security.Cryptography;
using System.Text;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Implementations;
using GraNAS.Signaling.Services.Interfaces;
using Moq;

namespace GraNAS.WebAPI.Tests.Unit;

public class AccessCheckerTests
{
    private readonly Mock<IMetadataServiceClient> _metadata = new();
    private readonly Mock<ISharingServiceClient> _sharing = new();
    private readonly AccessChecker _sut;

    public AccessCheckerTests()
    {
        _sut = new AccessChecker(_metadata.Object, _sharing.Object);
    }

    // ──────────── CheckJwtAccessAsync ────────────

    [Fact]
    public async Task CheckJwtAccess_UserHasAccess_ReturnsResult()
    {
        var folderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _metadata.Setup(m => m.GetFolderAccessAsync(folderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderAccessInfo(folderId, ownerId, null));

        var result = await _sut.CheckJwtAccessAsync(folderId, userId);

        Assert.NotNull(result);
        Assert.Equal(folderId, result.FolderId);
        Assert.Equal(ownerId, result.OwnerId);
        Assert.Null(result.ScopePath);
    }

    [Fact]
    public async Task CheckJwtAccess_WithScopePath_PropagatesScopePath()
    {
        var folderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _metadata.Setup(m => m.GetFolderAccessAsync(folderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderAccessInfo(folderId, userId, "docs/reports"));

        var result = await _sut.CheckJwtAccessAsync(folderId, userId);

        Assert.Equal("docs/reports", result!.ScopePath);
    }

    [Fact]
    public async Task CheckJwtAccess_NoAccess_ReturnsNull()
    {
        var folderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _metadata.Setup(m => m.GetFolderAccessAsync(folderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FolderAccessInfo?)null);

        var result = await _sut.CheckJwtAccessAsync(folderId, userId);

        Assert.Null(result);
    }

    // ──────────── CheckShareTokenAsync ────────────

    [Fact]
    public async Task CheckShareToken_ValidToken_ReturnsResult()
    {
        var folderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _sharing.Setup(s => s.GetShareByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareInfo(folderId, ownerId, null, DateTime.UtcNow.AddDays(7), false));

        var result = await _sut.CheckShareTokenAsync(folderId, "raw_token");

        Assert.NotNull(result);
        Assert.Equal(folderId, result.FolderId);
        Assert.Equal(ownerId, result.OwnerId);
    }

    [Fact]
    public async Task CheckShareToken_ExpiredToken_ReturnsNull()
    {
        var folderId = Guid.NewGuid();
        _sharing.Setup(s => s.GetShareByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareInfo(folderId, Guid.NewGuid(), null, DateTime.UtcNow.AddDays(-1), false));

        var result = await _sut.CheckShareTokenAsync(folderId, "token");

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckShareToken_RevokedToken_ReturnsNull()
    {
        var folderId = Guid.NewGuid();
        _sharing.Setup(s => s.GetShareByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareInfo(folderId, Guid.NewGuid(), null, DateTime.UtcNow.AddDays(7), true));

        var result = await _sut.CheckShareTokenAsync(folderId, "token");

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckShareToken_FolderIdMismatch_ReturnsNull()
    {
        var requestedFolderId = Guid.NewGuid();
        var differentFolderId = Guid.NewGuid();
        _sharing.Setup(s => s.GetShareByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareInfo(differentFolderId, Guid.NewGuid(), null, DateTime.UtcNow.AddDays(7), false));

        var result = await _sut.CheckShareTokenAsync(requestedFolderId, "token");

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckShareToken_TokenIsHashedBeforeQuery()
    {
        var folderId = Guid.NewGuid();
        var rawToken = "my_secret_share_token";
        string? capturedHash = null;

        _sharing.Setup(s => s.GetShareByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((h, _) => capturedHash = h)
            .ReturnsAsync((ShareInfo?)null);

        await _sut.CheckShareTokenAsync(folderId, rawToken);

        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        Assert.Equal(expectedHash, capturedHash);
    }
}
