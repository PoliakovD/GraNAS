using System.Reactive.Linq;
using FluentAssertions;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.Contracts.Metadata;
using Moq;

namespace GraNAS.Desktop.Tests;

public class RecentViewModelTests
{
    private readonly Mock<IFoldersApi> _foldersApi = new();
    private readonly Mock<INotificationService> _notifications = new();

    public RecentViewModelTests()
    {
        _foldersApi.Setup(f => f.GetFoldersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private RecentViewModel CreateSut() => new(_foldersApi.Object, _notifications.Object);

    [Fact]
    public async Task Load_PrefersLastAccessedAtOverUpdatedAt()
    {
        var now = DateTime.UtcNow;
        _foldersApi.Setup(f => f.GetFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FolderResponse { Id = Guid.NewGuid(), Name = "Recent", LastAccessedAt = now.AddMinutes(-5), UpdatedAt = now.AddDays(-10) },
                new FolderResponse { Id = Guid.NewGuid(), Name = "Old", LastAccessedAt = null, UpdatedAt = now.AddDays(-1) },
            ]);

        var vm = CreateSut();
        await vm.LoadCommand.Execute().FirstAsync();

        vm.RecentFolders[0].Name.Should().Be("Recent"); // lastAccessedAt 5 min ago wins over updatedAt 1 day ago
    }

    [Fact]
    public async Task Load_ExcludesFoldersWithNoTimestamps()
    {
        _foldersApi.Setup(f => f.GetFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FolderResponse { Id = Guid.NewGuid(), Name = "No dates", LastAccessedAt = null, UpdatedAt = null },
                new FolderResponse { Id = Guid.NewGuid(), Name = "Has date", LastAccessedAt = DateTime.UtcNow },
            ]);

        var vm = CreateSut();
        await vm.LoadCommand.Execute().FirstAsync();

        vm.RecentFolders.Should().ContainSingle(f => f.Name == "Has date");
    }

    [Fact]
    public async Task Load_CappsAt12Items()
    {
        var now = DateTime.UtcNow;
        var folders = Enumerable.Range(1, 20)
            .Select(i => new FolderResponse
            {
                Id = Guid.NewGuid(), Name = $"F{i}",
                UpdatedAt = now.AddHours(-i)
            })
            .ToList();
        _foldersApi.Setup(f => f.GetFoldersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folders);

        var vm = CreateSut();
        await vm.LoadCommand.Execute().FirstAsync();

        vm.RecentFolders.Should().HaveCount(12);
    }
}
