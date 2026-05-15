using System.Reactive.Linq;
using FluentAssertions;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.Contracts.Sharing;
using Moq;

namespace GraNAS.Desktop.Tests;

public class LinksViewModelTests
{
    private readonly Mock<ISharesApi> _sharesApi = new();
    private readonly Mock<IClipboardService> _clipboard = new();
    private readonly Mock<INotificationService> _notifications = new();

    public LinksViewModelTests()
    {
        _sharesApi.Setup(s => s.ListAllSharesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private LinksViewModel CreateSut() => new(_sharesApi.Object, _clipboard.Object, _notifications.Object);

    [Fact]
    public async Task Load_PopulatesLinks()
    {
        var links = new List<ShareLinkOwnerResponse>
        {
            new() { Id = Guid.NewGuid(), FolderName = "Folder A", Revoked = false },
            new() { Id = Guid.NewGuid(), FolderName = "Folder B", Revoked = true },
        };
        _sharesApi.Setup(s => s.ListAllSharesAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(links);

        var vm = CreateSut();
        await vm.LoadCommand.Execute().FirstAsync();

        vm.Links.Should().HaveCount(2);
    }

    [Fact]
    public async Task Revoke_RemovesLinkFromList()
    {
        var link = new ShareLinkOwnerResponse { Id = Guid.NewGuid(), FolderName = "X", Revoked = false };
        _sharesApi.Setup(s => s.ListAllSharesAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync([link]);
        _sharesApi.Setup(s => s.RevokeShareAsync(link.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var vm = CreateSut();
        await vm.LoadCommand.Execute().FirstAsync();
        await vm.RevokeCommand.Execute(link).FirstAsync();

        vm.Links.Should().BeEmpty();
        _notifications.Verify(n => n.Success(It.IsAny<string>(), null), Times.Once);
    }

    [Fact]
    public async Task Copy_CallsClipboard()
    {
        var link = new ShareLinkOwnerResponse { Id = Guid.NewGuid(), ShareUrl = "https://example.com/s/abc" };
        _clipboard.Setup(c => c.CopyAsync(link.ShareUrl)).Returns(Task.CompletedTask);

        var vm = CreateSut();
        await vm.CopyCommand.Execute(link).FirstAsync();

        _clipboard.Verify(c => c.CopyAsync(link.ShareUrl), Times.Once);
        _notifications.Verify(n => n.Success(It.IsAny<string>(), null), Times.Once);
    }
}
