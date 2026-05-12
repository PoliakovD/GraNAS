using FluentAssertions;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.P2P;
using Moq;

namespace GraNAS.Desktop.Tests;

public class P2PHostTests : IDisposable
{
    private readonly Mock<IFolderShareRegistry> _registry = new();
    private readonly Mock<IAuthSession> _session = new();
    private readonly Mock<ISignalingApi> _signalingApi = new();
    private readonly Mock<IDeviceIdentity> _identity = new();
    private readonly Mock<INotificationService> _notifications = new();

    private readonly Guid _myDeviceId = Guid.NewGuid();
    private readonly string _tempDir;

    public P2PHostTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("P2PHostTests").FullName;
        _identity.Setup(d => d.DeviceId).Returns(_myDeviceId);
        _registry.Setup(r => r.GetLocalPath(It.IsAny<Guid>())).Returns(_tempDir);
        // default: нет binding
        _signalingApi
            .Setup(a => a.GetFolderDevicesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync([]);
    }

    public void Dispose() => Directory.Delete(_tempDir, true);

    private TestableP2PHost CreateSut() =>
        new(_registry.Object, _session.Object, _signalingApi.Object, _identity.Object,
            _notifications.Object, "http://fake-hub");

    [Fact]
    public async Task HandleIncomingPeerRequest_BoundToOtherDevice_SendsAccessDeniedAndDoesNotStartWebRtc()
    {
        var folderId = Guid.NewGuid();
        var otherDeviceId = Guid.NewGuid();
        SetupBinding(folderId, otherDeviceId);

        var sut = CreateSut();
        await sut.HandleIncomingPeerRequestCoreAsync("recv-1", folderId, null);

        sut.DenyCalls.Should().ContainSingle()
            .Which.Should().Be(("recv-1", folderId, "folder_bound_to_another_device"));
        sut.StartCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleIncomingPeerRequest_BoundToCurrentDevice_StartsWebRtc()
    {
        var folderId = Guid.NewGuid();
        SetupBinding(folderId, _myDeviceId);

        var sut = CreateSut();
        await sut.HandleIncomingPeerRequestCoreAsync("recv-1", folderId, null);

        sut.StartCalls.Should().ContainSingle()
            .Which.Should().Be(("recv-1", folderId));
        sut.DenyCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleIncomingPeerRequest_NoBinding_StartsWebRtc_BackwardsCompat()
    {
        var folderId = Guid.NewGuid();
        // _signalingApi default: возвращает []

        var sut = CreateSut();
        await sut.HandleIncomingPeerRequestCoreAsync("recv-1", folderId, null);

        sut.StartCalls.Should().ContainSingle();
        sut.DenyCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleIncomingPeerRequest_CachesBindingLookup()
    {
        var folderId = Guid.NewGuid();
        SetupBinding(folderId, _myDeviceId);

        var sut = CreateSut();
        await sut.HandleIncomingPeerRequestCoreAsync("recv-1", folderId, null);
        await sut.HandleIncomingPeerRequestCoreAsync("recv-2", folderId, null);

        _signalingApi.Verify(
            a => a.GetFolderDevicesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default),
            Times.Once);
        sut.StartCalls.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleIncomingPeerRequest_MappingChanged_InvalidatesCache()
    {
        var folderId = Guid.NewGuid();
        SetupBinding(folderId, _myDeviceId);

        var sut = CreateSut();
        await sut.HandleIncomingPeerRequestCoreAsync("recv-1", folderId, null);

        _registry.Raise(r => r.MappingChanged += null, folderId);

        await sut.HandleIncomingPeerRequestCoreAsync("recv-2", folderId, null);

        _signalingApi.Verify(
            a => a.GetFolderDevicesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleIncomingPeerRequest_NoLocalPath_NoApiCallNoDeny()
    {
        var folderId = Guid.NewGuid();
        _registry.Setup(r => r.GetLocalPath(folderId)).Returns((string?)null);

        var sut = CreateSut();
        await sut.HandleIncomingPeerRequestCoreAsync("recv-1", folderId, null);

        _signalingApi.Verify(
            a => a.GetFolderDevicesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default),
            Times.Never);
        sut.DenyCalls.Should().BeEmpty();
        sut.StartCalls.Should().BeEmpty();
    }

    private void SetupBinding(Guid folderId, Guid deviceId)
    {
        _signalingApi
            .Setup(a => a.GetFolderDevicesAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(folderId)), default))
            .ReturnsAsync([new FolderDeviceBinding(folderId, deviceId, "PC", "Windows", true, DateTime.UtcNow)]);
    }
}

/// <summary>
/// Тестовый subclass с заглушками вместо реального WebRTC и SignalR hub-вызовов.
/// </summary>
internal sealed class TestableP2PHost : P2PHost
{
    public List<(string ConnId, Guid FolderId, string Reason)> DenyCalls { get; } = [];
    public List<(string ConnId, Guid FolderId)> StartCalls { get; } = [];

    public TestableP2PHost(
        IFolderShareRegistry registry,
        IAuthSession session,
        ISignalingApi signalingApi,
        IDeviceIdentity deviceIdentity,
        INotificationService notifications,
        string hubUrl)
        : base(registry, session, signalingApi, deviceIdentity, notifications, hubUrl) { }

    protected internal override Task SendDenyAsync(string receiverConnId, Guid folderId, string reason)
    {
        DenyCalls.Add((receiverConnId, folderId, reason));
        return Task.CompletedTask;
    }

    protected internal override Task StartWebRtcSessionAsync(
        string receiverConnId, Guid folderId, string? scopePath, string localPath)
    {
        StartCalls.Add((receiverConnId, folderId));
        return Task.CompletedTask;
    }
}
