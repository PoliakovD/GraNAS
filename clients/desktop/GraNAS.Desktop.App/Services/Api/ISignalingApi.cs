namespace GraNAS.Desktop.App.Services.Api;

public record TurnCredentials(string Username, string Credential, string[] Uris, int Ttl);

public record DeviceRegistrationRequest(Guid DeviceId, string DeviceName, string Platform);

public record DeviceResponse(Guid DeviceId, string DeviceName, string Platform,
    DateTime CreatedAt, DateTime LastSeenAt, bool IsOnline);

public record ActiveSessionResponse(Guid DeviceId, string DeviceName, string Platform,
    string Ip, DateTime ConnectedAt);

public interface ISignalingApi
{
    Task<TurnCredentials?> GetTurnCredentialsAsync(CancellationToken ct = default);
    Task<DeviceResponse?> RegisterDeviceAsync(DeviceRegistrationRequest req, CancellationToken ct = default);
    Task<List<DeviceResponse>> GetDevicesAsync(CancellationToken ct = default);
    Task<List<ActiveSessionResponse>> GetActiveSessionsAsync(CancellationToken ct = default);
    Task TerminateSessionAsync(Guid deviceId, CancellationToken ct = default);
}
