using System.Reactive.Linq;
using FluentAssertions;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.P2P;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.Contracts.Common;
using Moq;

namespace GraNAS.Desktop.Tests;

public class SettingsViewModelTests
{
    private readonly Mock<IAuthSession> _session = new();
    private readonly Mock<ISignalingApi> _signalingApi = new();
    private readonly Mock<IDeviceIdentity> _identity = new();
    private readonly Mock<INotificationService> _notifications = new();

    private readonly Guid _deviceId = Guid.NewGuid();

    public SettingsViewModelTests()
    {
        _session.Setup(s => s.CurrentUserId).Returns(Guid.NewGuid());
        _session.Setup(s => s.CurrentUserEmail).Returns("test@test.com");
        _identity.Setup(d => d.DeviceId).Returns(_deviceId);
        _identity.Setup(d => d.DeviceName).Returns("MyPC");
        _identity.Setup(d => d.Platform).Returns("windows");
    }

    private SettingsViewModel CreateSut() => new(
        _session.Object, _signalingApi.Object, _identity.Object, _notifications.Object);

    [Fact]
    public async Task SaveCommand_CannotExecute_WhenNameEmpty()
    {
        var vm = CreateSut();
        vm.DeviceName = "";

        var canExecute = await vm.SaveCommand.CanExecute.FirstAsync();

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_CannotExecute_WhenNameTooLong()
    {
        var vm = CreateSut();
        vm.DeviceName = new string('A', 101);

        var canExecute = await vm.SaveCommand.CanExecute.FirstAsync();

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task Save_SameName_ShowsInfoAndDoesNotCallApi()
    {
        var vm = CreateSut();
        // DeviceName initialized to "MyPC" from identity mock
        vm.DeviceName = "MyPC";

        await vm.SaveCommand.Execute().FirstAsync();

        _signalingApi.Verify(s => s.RenameDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(n => n.Info(It.IsAny<string>(), null), Times.Once);
    }

    [Fact]
    public async Task Save_Success_UpdatesDeviceIdentityAndShowsSuccess()
    {
        var newName = "WorkLaptop";
        var updatedResponse = new DeviceResponse(_deviceId, newName, "Windows",
            DateTime.UtcNow, DateTime.UtcNow, true);

        _signalingApi.Setup(s => s.RenameDeviceAsync(_deviceId, newName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedResponse);

        var vm = CreateSut();
        vm.DeviceName = newName;

        await vm.SaveCommand.Execute().FirstAsync();

        _identity.Verify(d => d.SetDeviceName(newName), Times.Once);
        _notifications.Verify(n => n.Success(It.IsAny<string>(), null), Times.Once);
        vm.DeviceName.Should().Be(newName);
    }

    [Fact]
    public async Task Save_409Conflict_RevertsNameAndShowsError()
    {
        var newName = "ConflictName";
        _signalingApi.Setup(s => s.RenameDeviceAsync(_deviceId, newName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(409, new ErrorResponse { Error = "NAME_CONFLICT" }));

        var vm = CreateSut();
        vm.DeviceName = newName;

        await vm.SaveCommand.Execute().FirstAsync();

        _identity.Verify(d => d.SetDeviceName(It.IsAny<string>()), Times.Never);
        _notifications.Verify(n => n.Error(It.IsAny<string>(), null), Times.Once);
        vm.DeviceName.Should().Be("MyPC"); // reverted to original
    }
}
