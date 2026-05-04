using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.API.Infrastructure;

public class MetadataServiceClient : IMetadataServiceClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<MetadataServiceClient> _logger;

    public MetadataServiceClient(HttpClient http, IHttpContextAccessor accessor, ILogger<MetadataServiceClient> logger)
    {
        _http = http;
        _accessor = accessor;
        _logger = logger;
    }

    public async Task<FolderAccessInfo?> GetFolderAccessAsync(Guid folderId, Guid userId, CancellationToken ct = default)
    {
        var path = $"api/internal/folders/{folderId}/access?userId={userId}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        ForwardAuthorization(request);

        try
        {
            var response = await _http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MetadataClient: GET {Path} returned {StatusCode}", path, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<FolderAccessInfo>(ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "MetadataClient: request to {Path} failed", path);
            throw;
        }
    }

    private void ForwardAuthorization(HttpRequestMessage request)
    {
        var auth = _accessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(auth))
        {
            request.Headers.TryAddWithoutValidation("Authorization", auth);
            return;
        }

        // SignalR WebSocket sends JWT via ?access_token= query param, not Authorization header
        var token = _accessor.HttpContext?.Request.Query["access_token"].ToString();
        if (!string.IsNullOrEmpty(token))
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
    }
}
