using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Models.Enums;
using GraNAS.Signaling.Models.Repositories;
using GraNAS.Signaling.Services.Implementations;
using GraNAS.Signaling.Services.Interfaces;
using Moq;

namespace GraNAS.WebAPI.Tests.Unit;

public class DeviceServiceTests
{
    private readonly Mock<IDeviceRepository> _repoMock = new();
    private readonly Mock<ISessionStore> _sessionsMock = new();
    private DeviceService CreateSut() => new(_repoMock.Object, _sessionsMock.Object);

    [Fact]
    public async Task RegisterOrUpdateAsync_NewDevice_ReturnsResponse()
    {
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var req = new DeviceRegistrationRequest
        {
            DeviceId = deviceId,
            DeviceName = "TestPC",
            Platform = DevicePlatform.Windows
        };
        var device = new Device
        {
            Id = deviceId,
            UserId = userId,
            DeviceName = "TestPC",
            Platform = DevicePlatform.Windows,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.UpsertAsync(It.IsAny<Device>(), default)).ReturnsAsync(device);
        _sessionsMock.Setup(s => s.IsDeviceOnlineAsync(deviceId, default)).ReturnsAsync(false);

        var result = await CreateSut().RegisterOrUpdateAsync(userId, req);

        Assert.Equal(deviceId, result.DeviceId);
        Assert.Equal("TestPC", result.DeviceName);
        Assert.Equal(DevicePlatform.Windows, result.Platform);
        Assert.False(result.IsOnline);
    }

    [Fact]
    public async Task RegisterOrUpdateAsync_DifferentUser_ThrowsInvalidOperation()
    {
        var req = new DeviceRegistrationRequest
        {
            DeviceId = Guid.NewGuid(),
            DeviceName = "PC",
            Platform = DevicePlatform.Windows
        };

        _repoMock.Setup(r => r.UpsertAsync(It.IsAny<Device>(), default))
            .ThrowsAsync(new InvalidOperationException("Device belongs to a different user."));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().RegisterOrUpdateAsync(Guid.NewGuid(), req));
    }

    [Fact]
    public async Task RegisterOrUpdateAsync_OnlineDevice_SetsIsOnline()
    {
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var device = new Device { Id = deviceId, UserId = userId, DeviceName = "PC", Platform = DevicePlatform.Windows };

        _repoMock.Setup(r => r.UpsertAsync(It.IsAny<Device>(), default)).ReturnsAsync(device);
        _sessionsMock.Setup(s => s.IsDeviceOnlineAsync(deviceId, default)).ReturnsAsync(true);

        var result = await CreateSut().RegisterOrUpdateAsync(userId, new DeviceRegistrationRequest
        {
            DeviceId = deviceId, DeviceName = "PC", Platform = DevicePlatform.Windows
        });

        Assert.True(result.IsOnline);
    }

    [Fact]
    public async Task GetByUserAsync_FiltersCorrectly()
    {
        var userId = Guid.NewGuid();
        var devices = new List<Device>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, DeviceName = "A", Platform = DevicePlatform.Windows },
            new() { Id = Guid.NewGuid(), UserId = userId, DeviceName = "B", Platform = DevicePlatform.Linux }
        };

        _repoMock.Setup(r => r.GetByUserAsync(userId, default)).ReturnsAsync(devices);
        _sessionsMock.Setup(s => s.IsDeviceOnlineAsync(It.IsAny<Guid>(), default)).ReturnsAsync(false);

        var result = await CreateSut().GetByUserAsync(userId);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task BelongsToUserAsync_DelegatesCorrectly()
    {
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.BelongsToUserAsync(deviceId, userId, default)).ReturnsAsync(true);

        var result = await CreateSut().BelongsToUserAsync(deviceId, userId);
        Assert.True(result);
    }
}
