using System.Reactive.Linq;
using FluentAssertions;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.Contracts.Metadata;
using Moq;

namespace GraNAS.Desktop.Tests;

public class FolderPropertiesViewModelTests
{
    private readonly Mock<ISignalingApi> _signalingApi = new();
    private readonly Mock<IAuthSession> _session = new();
    private readonly Mock<INotificationService> _notifications = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _folderId = Guid.NewGuid();

    public FolderPropertiesViewModelTests()
    {
        _session.Setup(s => s.CurrentUserId).Returns(_userId);
        _signalingApi.Setup(s => s.GetFolderDevicesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private FolderResponse MakeFolder(bool owned = true) => new()
    {
        Id = _folderId,
        Name = "TestFolder",
        OwnerId = owned ? _userId : Guid.NewGuid(),
        OwnerEmail = owned ? null : "other@test.com",
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private FolderPropertiesViewModel CreateSut(FolderResponse folder) =>
        new(folder, _signalingApi.Object, _session.Object, _notifications.Object);

    [Fact]
    public void FolderIdShort_IsFirst8Chars()
    {
        var vm = CreateSut(MakeFolder());
        vm.FolderIdShort.Should().EndWith("…").And.HaveLength(9);
    }

    [Fact]
    public void IsOwner_TrueWhenOwnerMatches()
    {
        var vm = CreateSut(MakeFolder(owned: true));
        vm.IsOwner.Should().BeTrue();
    }

    [Fact]
    public void IsOwner_FalseWhenOwnerDiffers()
    {
        var vm = CreateSut(MakeFolder(owned: false));
        vm.IsOwner.Should().BeFalse();
    }

    [Fact]
    public async Task LoadCommand_SetsDeviceInfo_WhenBindingExists()
    {
        var deviceId = Guid.NewGuid();
        var binding = new FolderDeviceBinding(_folderId, deviceId, "MyPC", "Windows", true, DateTime.UtcNow);
        _signalingApi.Setup(s => s.GetFolderDevicesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([binding]);

        var vm = CreateSut(MakeFolder());
        await vm.LoadCommand.Execute().FirstAsync();

        vm.HasDevice.Should().BeTrue();
        vm.DeviceName.Should().Be("MyPC");
        vm.DeviceIsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseDeviceCommand_CallsApiAndClearsDevice()
    {
        var deviceId = Guid.NewGuid();
        var binding = new FolderDeviceBinding(_folderId, deviceId, "MyPC", "Windows", false, DateTime.UtcNow);
        _signalingApi.Setup(s => s.GetFolderDevicesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([binding]);
        _signalingApi.Setup(s => s.ReleaseFolderAsync(deviceId, _folderId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vm = CreateSut(MakeFolder());
        await vm.LoadCommand.Execute().FirstAsync();
        await vm.ReleaseDeviceCommand.Execute().FirstAsync();

        _signalingApi.Verify(s => s.ReleaseFolderAsync(deviceId, _folderId, It.IsAny<CancellationToken>()), Times.Once);
        vm.HasDevice.Should().BeFalse();
        vm.DeviceName.Should().BeNull();
    }
}
