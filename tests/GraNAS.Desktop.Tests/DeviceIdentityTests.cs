using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.P2P;
using Moq;

namespace GraNAS.Desktop.Tests;

public class DeviceIdentityTests
{
    [Fact]
    public void FirstCall_GeneratesAndSavesDeviceId()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.Get("deviceId")).Returns((string?)null);

        var identity = new DeviceIdentity(store.Object);

        Assert.NotEqual(Guid.Empty, identity.DeviceId);
        store.Verify(s => s.Save("deviceId", identity.DeviceId.ToString()), Times.Once);
    }

    [Fact]
    public void SubsequentCall_ReadsExistingDeviceId()
    {
        var existingId = Guid.NewGuid();
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.Get("deviceId")).Returns(existingId.ToString());

        var identity = new DeviceIdentity(store.Object);

        Assert.Equal(existingId, identity.DeviceId);
        store.Verify(s => s.Save(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void DeviceName_EqualsEnvironmentMachineName()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(s => s.Get("deviceId")).Returns(Guid.NewGuid().ToString());

        var identity = new DeviceIdentity(store.Object);

        Assert.Equal(Environment.MachineName, identity.DeviceName);
    }
}
