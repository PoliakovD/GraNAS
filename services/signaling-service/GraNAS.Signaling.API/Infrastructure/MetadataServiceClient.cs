using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GraNAS.Signaling.API.Infrastructure;

public class MetadataServiceClient : IMetadataServiceClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;

    public MetadataServiceClient(HttpClient http, IHttpContextAccessor accessor)
    {
        _http = http;
        _accessor = accessor;
    }

    public async Task<FolderAccessInfo?> GetFolderAccessAsync(Guid folderId, Guid userId, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/internal/folders/{folderId}/access?userId={userId}");
        ForwardAuthorization(request);
        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FolderAccessInfo>(ct);
    }

    private void ForwardAuthorization(HttpRequestMessage request)
    {
        var auth = _accessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(auth))
            request.Headers.TryAddWithoutValidation("Authorization", auth);
    }
}
