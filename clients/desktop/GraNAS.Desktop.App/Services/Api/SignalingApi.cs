using System.Net.Http.Json;

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

    public async Task<FolderDeviceConflict?> ClaimFolderAsync(Guid deviceId, Guid folderId, bool force = false, CancellationToken ct = default)
    {
        try
        {
            var url = $"api/signaling/devices/{deviceId}/folders/{folderId}{(force ? "?force=true" : "")}";
            var response = await Http.PostAsync(url, null, ct);
            if ((int)response.StatusCode == 409)
                return await response.Content.ReadFromJsonAsync<FolderDeviceConflict>(cancellationToken: ct);
            return null; // 204 NoContent = success
        }
        catch { return null; }
    }

    public async Task ReleaseFolderAsync(Guid deviceId, Guid folderId, CancellationToken ct = default)
    {
        try { await DeleteAsync($"api/signaling/devices/{deviceId}/folders/{folderId}", ct); }
        catch { /* best-effort */ }
    }
}
