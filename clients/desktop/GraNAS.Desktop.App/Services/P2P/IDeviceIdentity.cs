namespace GraNAS.Desktop.App.Services.P2P;

public interface IDeviceIdentity
{
    Guid DeviceId { get; }
    string DeviceName { get; }
    string Platform { get; }
}
