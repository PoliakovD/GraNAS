using GraNAS.Desktop.App.Services.Auth;

namespace GraNAS.Desktop.App.Services.P2P;

public class DeviceIdentity : IDeviceIdentity
{
    private const string DeviceIdKey = "deviceId";

    private readonly ICredentialStore _store;

    public Guid DeviceId { get; }
    public string DeviceName { get; }
    public string Platform => "windows";

    public DeviceIdentity(ICredentialStore store)
    {
        _store = store;
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

    public bool IsRegisteredForUser(Guid userId) =>
        _store.Get($"deviceRegistered:{DeviceId}:{userId}") == "1";

    public void MarkRegisteredForUser(Guid userId) =>
        _store.Save($"deviceRegistered:{DeviceId}:{userId}", "1");
}
