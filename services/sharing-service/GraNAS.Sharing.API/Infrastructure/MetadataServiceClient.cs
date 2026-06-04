using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Sharing.Models.DTO;
using GraNAS.Sharing.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GraNAS.Sharing.API.Infrastructure;

/// <summary>
/// HTTP-клиент к metadata-service для межсервисных вызовов.
/// Использует сервисный JWT (подписан тем же ключом), а не JWT пользователя,
/// чтобы работать в том числе при анонимных запросах (share-ссылки без авторизации).
/// </summary>
public class MetadataServiceClient : IMetadataServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<MetadataServiceClient> _logger;
    private readonly byte[] _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public MetadataServiceClient(HttpClient http, IConfiguration configuration, ILogger<MetadataServiceClient> logger)
    {
        _http = http;
        _logger = logger;
        var jwtSettings = configuration.GetSection("Jwt");
        _jwtKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
        _jwtIssuer = jwtSettings["Issuer"]!;
        _jwtAudience = jwtSettings["Audience"]!;
    }

    public async Task<FolderInfo?> GetFolderForOwnerAsync(Guid folderId, Guid ownerId, CancellationToken ct = default)
    {
        var path = $"api/internal/folders/{folderId}/owner/{ownerId}";
        return await SendAsync<FolderInfo>(path, ct);
    }

    public async Task<FolderInfo?> GetFolderAsync(Guid folderId, CancellationToken ct = default)
    {
        var path = $"api/internal/folders/{folderId}";
        return await SendAsync<FolderInfo>(path, ct);
    }

    private async Task<T?> SendAsync<T>(string path, CancellationToken ct) where T : class
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {GetServiceToken()}");

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

            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
                _logger.LogDebug("MetadataClient: request to {Path} canceled (client disconnected)", path);
            else
                _logger.LogError(ex, "MetadataClient: request to {Path} failed", path);
            return null;
        }
    }

    private string GetServiceToken()
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        var expiry = DateTime.UtcNow.AddHours(1);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_jwtKey),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            expires: expiry,
            signingCredentials: credentials);

        _cachedToken = new JwtSecurityTokenHandler().WriteToken(token);
        _tokenExpiresAt = expiry.AddMinutes(-5);
        return _cachedToken;
    }
}
