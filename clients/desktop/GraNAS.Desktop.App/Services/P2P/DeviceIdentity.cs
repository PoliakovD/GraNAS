using GraNAS.Desktop.App.Services.Auth;

namespace GraNAS.Desktop.App.Services.P2P;

public class DeviceIdentity : IDeviceIdentity
{
    private const string DeviceIdKey = "deviceId";

    public Guid DeviceId { get; }
    public string DeviceName { get; }
    public string Platform => "windows";

    public DeviceIdentity(ICredentialStore store)
    {
        var saved = store.Get(DeviceIdKey);
        if (Guid.TryParse(saved, out var existing))
        {
            DeviceId = existing;
        }
        else
        {
            DeviceId = Guid.NewGuid();
            store.Save(DeviceIdKey, DeviceId.ToString());
        }
        DeviceName = Environment.MachineName;
    }
}
