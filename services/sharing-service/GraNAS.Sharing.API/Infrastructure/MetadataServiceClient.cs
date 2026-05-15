using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Sharing.Models.DTO;
using GraNAS.Sharing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GraNAS.Sharing.API.Infrastructure;

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

    public async Task<FolderInfo?> GetFolderForOwnerAsync(Guid folderId, Guid ownerId, CancellationToken ct = default)
    {
        var path = $"api/internal/folders/{folderId}/owner/{ownerId}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        ForwardAuthorization(request);

        try
        {
            var response = await _http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MetadataClient: GET {Path} returned {StatusCode}", path, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<FolderInfo>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogError(ex, "MetadataClient: request to {Path} failed", path);
            return null;
        }
    }

    public async Task<FolderInfo?> GetFolderAsync(Guid folderId, CancellationToken ct = default)
    {
        var path = $"api/internal/folders/{folderId}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        ForwardAuthorization(request);

        try
        {
            var response = await _http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MetadataClient: GET {Path} returned {StatusCode}", path, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<FolderInfo>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogError(ex, "MetadataClient: request to {Path} failed", path);
            return null;
        }
    }

    private void ForwardAuthorization(HttpRequestMessage request)
    {
        var authorization = _accessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
    }
}
