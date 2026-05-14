using System.Reactive.Linq;
using FluentAssertions;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.P2P;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.Contracts.Metadata;
using Moq;

namespace GraNAS.Desktop.Tests;

public class MyFoldersViewModelTests
{
    private readonly Mock<IFoldersApi> _foldersApi = new();
    private readonly Mock<IAuthSession> _session = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IFolderShareRegistry> _registry = new();
    private readonly Mock<IP2PHost> _p2pHost = new();
    private readonly Mock<ISignalingApi> _signalingApi = new();
    private readonly Mock<IDeviceIdentity> _identity = new();

    private readonly Guid _deviceId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public MyFoldersViewModelTests()
    {
        _identity.Setup(d => d.DeviceId).Returns(_deviceId);
        _session.Setup(s => s.CurrentUserId).Returns(_userId);
        // Default: empty folders and no bindings
        _foldersApi.Setup(f => f.GetFoldersAsync(default)).ReturnsAsync([]);
        _signalingApi.Setup(s => s.GetDeviceFoldersAsync(_deviceId, default)).ReturnsAsync([]);
        _registry.Setup(r => r.GetAll()).Returns(new Dictionary<Guid, string>());
    }

    private MyFoldersViewModel CreateSut() => new(
        _foldersApi.Object,
        _session.Object,
        _dialogs.Object,
        _notifications.Object,
        _registry.Object,
        _p2pHost.Object,
        _signalingApi.Object,
        _identity.Object);

    [Fact]
    public async Task LoadCommand_LocalBindings_PopulatedFromServerAndRegistry()
    {
        var folderId = Guid.NewGuid();
        var localPath = @"C:\MyFolder";
        var folder = new FolderResponse { Id = folderId, Name = "MyFolder", OwnerId = _userId };

        _foldersApi.Setup(f => f.GetFoldersAsync(default)).ReturnsAsync([folder]);
        _signalingApi.Setup(s => s.GetDeviceFoldersAsync(_deviceId, default))
            .ReturnsAsync([new DeviceFolderInfo(folderId, DateTime.UtcNow)]);
        _registry.Setup(r => r.GetAll())
            .Returns(new Dictionary<Guid, string> { [folderId] = localPath });

        var vm = CreateSut();
        await vm.LoadCommand.Execute().FirstAsync();

        vm.LocalBindings.Should().ContainSingle(b =>
            b.FolderId == folderId &&
            b.FolderName == "MyFolder" &&
            b.LocalPath == localPath);
    }

    [Fact]
    public async Task ReleaseBindingCommand_CallsApiAndRegistry()
    {
        var folderId = Guid.NewGuid();
        var row = new LocalBindingRow(folderId, "TestFolder", @"C:\Test");

        _signalingApi.Setup(s => s.ReleaseFolderAsync(_deviceId, folderId, default))
            .Returns(Task.CompletedTask);

        var vm = CreateSut();
        // Manually add the row to LocalBindings
        vm.LocalBindings.Add(row);

        await vm.ReleaseBindingCommand.Execute(row).FirstAsync();

        _signalingApi.Verify(s => s.ReleaseFolderAsync(_deviceId, folderId, default), Times.Once);
        _registry.Verify(r => r.RemoveMapping(folderId), Times.Once);
        vm.LocalBindings.Should().BeEmpty();
    }
}
