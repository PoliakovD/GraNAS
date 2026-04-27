using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Interfaces;

namespace GraNAS.Signaling.API.Infrastructure;

public class SharingServiceClient : ISharingServiceClient
{
    private readonly HttpClient _http;

    public SharingServiceClient(HttpClient http) => _http = http;

    public async Task<ShareInfo?> GetShareByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/internal/shares/by-token-hash/{tokenHash}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShareInfo>(ct);
    }
}
