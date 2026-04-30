namespace GraNAS.Desktop.App.Services.Api;

public class SignalingApi : ApiBase, ISignalingApi
{
    public SignalingApi(HttpClient http) : base(http) { }

    public async Task<TurnCredentials?> GetTurnCredentialsAsync(CancellationToken ct = default)
    {
        try { return await GetAsync<TurnCredentials>("api/signaling/turn/credentials", ct); }
        catch { return null; }
    }

    public async Task<DeviceResponse?> RegisterDeviceAsync(DeviceRegistrationRequest req, CancellationToken ct = default)
    {
        try { return await PostAsync<DeviceResponse>("api/signaling/devices", req, ct); }
        catch { return null; }
    }

    public async Task<List<DeviceResponse>> GetDevicesAsync(CancellationToken ct = default)
    {
        try { return await GetAsync<List<DeviceResponse>>("api/signaling/devices", ct); }
        catch { return []; }
    }

    public async Task<List<ActiveSessionResponse>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        try { return await GetAsync<List<ActiveSessionResponse>>("api/signaling/sessions", ct); }
        catch { return []; }
    }

    public async Task TerminateSessionAsync(Guid deviceId, CancellationToken ct = default)
    {
        try { await DeleteAsync($"api/signaling/sessions/{deviceId}", ct); }
        catch { /* best-effort */ }
    }
}
