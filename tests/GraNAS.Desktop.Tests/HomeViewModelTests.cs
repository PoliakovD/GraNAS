using System.Reactive.Linq;
using FluentAssertions;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.Contracts.Metadata;
using GraNAS.Desktop.Contracts.Sharing;
using Moq;

namespace GraNAS.Desktop.Tests;

public class HomeViewModelTests
{
    private readonly Mock<IFoldersApi> _foldersApi = new();
    private readonly Mock<ISharesApi> _sharesApi = new();
    private readonly Mock<IAuthSession> _session = new();
    private readonly Mock<INotificationService> _notifications = new();

    private readonly Guid _userId = Guid.NewGuid();

    public HomeViewModelTests()
    {
        _session.Setup(s => s.CurrentUserId).Returns(_userId);
        _foldersApi.Setup(f => f.GetFoldersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _sharesApi.Setup(s => s.ListAllSharesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private HomeViewModel CreateSut() => new(_foldersApi.Object, _sharesApi.Object, _session.Object, _notifications.Object);

    [Fact]
    public async Task Load_CountsOwnedAndSharedCorrectly()
    {
        var other = Guid.NewGuid();
        _foldersApi.Setup(f => f.GetFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FolderResponse { Id = Guid.NewGuid(), Name = "A", OwnerId = _userId },
                new FolderResponse { Id = Guid.NewGuid(), Name = "B", OwnerId = _userId },
                new FolderResponse { Id = Guid.NewGuid(), Name = "C", OwnerId = other },
            ]);

        var vm = CreateSut();
        await vm.LoadCommand.Execute().FirstAsync();

        vm.OwnedCount.Should().Be(2);
        vm.SharedCount.Should().Be(1);
        vm.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Load_CountsActiveLinks()
    {
        _sharesApi.Setup(s => s.ListAllSharesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ShareLinkOwnerResponse { Id = Guid.NewGuid(), Revoked = false },
                new ShareLinkOwnerResponse { Id = Guid.NewGuid(), Revoked = true },
                new ShareLinkOwnerResponse { Id = Guid.NewGuid(), Revoked = false },
            ]);

        var vm = CreateSut();
        await vm.LoadCommand.Execute().FirstAsync();

        vm.ActiveLinksCount.Should().Be(2);
    }

    [Fact]
    public async Task Load_RecentFolders_Top6SortedByUpdatedAt()
    {
        var now = DateTime.UtcNow;
        var folders = Enumerable.Range(1, 8)
            .Select(i => new FolderResponse
            {
                Id = Guid.NewGuid(),
                Name = $"F{i}",
                OwnerId = _userId,
                UpdatedAt = now.AddHours(-i),
            })
            .ToList();

        _foldersApi.Setup(f => f.GetFoldersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folders);

        var vm = CreateSut();
        await vm.LoadCommand.Execute().FirstAsync();

        vm.RecentFolders.Should().HaveCount(6);
        vm.RecentFolders[0].Name.Should().Be("F1"); // most recently updated
    }
}
