using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Sharing.Models.DTO;
using GraNAS.Sharing.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GraNAS.Sharing.API.Infrastructure;

public class MetadataServiceClient : IMetadataServiceClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;

    public MetadataServiceClient(HttpClient http, IHttpContextAccessor accessor)
    {
        _http = http;
        _accessor = accessor;
    }

    public async Task<FolderInfo?> GetFolderForOwnerAsync(Guid folderId, Guid ownerId, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/internal/folders/{folderId}/owner/{ownerId}");

        ForwardAuthorization(request);

        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FolderInfo>(ct);
    }

    public async Task<FolderInfo?> GetFolderAsync(Guid folderId, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/internal/folders/{folderId}");

        ForwardAuthorization(request);

        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FolderInfo>(ct);
    }

    private void ForwardAuthorization(HttpRequestMessage request)
    {
        var authorization = _accessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
    }
}
