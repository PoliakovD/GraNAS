using System.Net.Http.Json;

namespace GraNAS.Desktop.App.Services.Api;

/// <summary>
/// Реализация REST-клиента к signaling-service через api-gateway.
/// Все методы перехватывают исключения и возвращают fallback-значения
/// (null или пустые коллекции), чтобы не нарушать работу P2P при временной недоступности.
/// </summary>
public class SignalingApi : ApiBase, ISignalingApi
{
    public SignalingApi(HttpClient http) : base(http) { }

    /// <inheritdoc/>
    public async Task<TurnCredentials?> GetTurnCredentialsAsync(CancellationToken ct = default)
    {
        try { return await GetAsync<TurnCredentials>("api/signaling/turn/credentials", ct); }
        catch { return null; }
    }

    /// <inheritdoc/>
    public async Task<DeviceResponse?> RegisterDeviceAsync(DeviceRegistrationRequest req, CancellationToken ct = default)
    {
        try { return await PostAsync<DeviceResponse>("api/signaling/devices", req, ct); }
        catch { return null; }
    }

    /// <inheritdoc/>
    public async Task<List<DeviceResponse>> GetDevicesAsync(CancellationToken ct = default)
    {
        try { return await GetAsync<List<DeviceResponse>>("api/signaling/devices", ct); }
        catch { return []; }
    }

    /// <inheritdoc/>
    public async Task<List<ActiveSessionResponse>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        try { return await GetAsync<List<ActiveSessionResponse>>("api/signaling/sessions", ct); }
        catch { return []; }
    }

    /// <inheritdoc/>
    public async Task TerminateSessionAsync(Guid deviceId, CancellationToken ct = default)
    {
        try { await DeleteAsync($"api/signaling/sessions/{deviceId}", ct); }
        catch { /* best-effort */ }
    }

    /// <inheritdoc/>
    /// <remarks>HTTP 204 → успех (<c>null</c>); HTTP 409 → десериализует тело ответа в <see cref="FolderDeviceConflict"/>.</remarks>
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

    /// <inheritdoc/>
    public async Task ReleaseFolderAsync(Guid deviceId, Guid folderId, CancellationToken ct = default)
    {
        try { await DeleteAsync($"api/signaling/devices/{deviceId}/folders/{folderId}", ct); }
        catch { /* best-effort */ }
    }

    /// <inheritdoc/>
    public async Task<List<FolderDeviceBinding>> GetFolderDevicesAsync(IReadOnlyCollection<Guid> folderIds, CancellationToken ct = default)
    {
        if (folderIds.Count == 0) return [];
        try
        {
            var query = string.Join("&", folderIds.Select(id => $"folderIds={id}"));
            return await GetAsync<List<FolderDeviceBinding>>($"api/signaling/devices/folder-devices?{query}", ct);
        }
        catch { return []; }
    }

    /// <inheritdoc/>
    public async Task<List<DeviceFolderInfo>> GetDeviceFoldersAsync(Guid deviceId, CancellationToken ct = default)
    {
        try { return await GetAsync<List<DeviceFolderInfo>>($"api/signaling/devices/{deviceId}/folders", ct); }
        catch { return []; }
    }

    /// <inheritdoc/>
    public async Task<DeviceResponse> RenameDeviceAsync(Guid deviceId, string deviceName, CancellationToken ct = default)
    {
        return await PatchAsync<DeviceResponse>(
            $"api/signaling/devices/{deviceId}",
            new DeviceRenameRequest(deviceName),
            ct);
    }
}
