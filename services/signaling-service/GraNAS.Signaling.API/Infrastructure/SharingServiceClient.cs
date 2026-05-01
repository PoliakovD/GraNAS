using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.API.Infrastructure;

public class SharingServiceClient : ISharingServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SharingServiceClient> _logger;

    public SharingServiceClient(HttpClient http, ILogger<SharingServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ShareInfo?> GetShareByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var path = $"api/internal/shares/by-token-hash/{tokenHash}";
        try
        {
            var response = await _http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SharingClient: GET {Path} returned {StatusCode}", path, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ShareInfo>(ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "SharingClient: request to {Path} failed", path);
            throw;
        }
    }
}
