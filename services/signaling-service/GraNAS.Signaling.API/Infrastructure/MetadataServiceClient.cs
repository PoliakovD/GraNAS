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

/// <summary>
/// HTTP-клиент к metadata-service для проверки прав доступа пользователя к папке.
/// Автоматически пробрасывает JWT из текущего HTTP-запроса, в том числе из query-параметра
/// <c>access_token</c>, который SignalR использует при WebSocket-подключении.
/// </summary>
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

    /// <inheritdoc/>
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

    /// <summary>
    /// Проксирует JWT текущего запроса в исходящий HTTP-запрос к metadata-service.
    /// При обычных REST-запросах берёт заголовок <c>Authorization</c>.
    /// При SignalR WebSocket-подключениях JWT передаётся в query-параметре <c>?access_token=</c>,
    /// поэтому метод проверяет оба варианта.
    /// </summary>
    private void ForwardAuthorization(HttpRequestMessage request)
    {
        var auth = _accessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(auth))
        {
            request.Headers.TryAddWithoutValidation("Authorization", auth);
            return;
        }

        // SignalR WebSocket передаёт JWT через ?access_token=, а не через заголовок Authorization
        var token = _accessor.HttpContext?.Request.Query["access_token"].ToString();
        if (!string.IsNullOrEmpty(token))
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
    }
}
