using System.Net.Http.Json;
using GraNAS.Notifications.Services.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GraNAS.Notifications.Services.Implementations;

public class AuthServiceClient
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthServiceClient> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public AuthServiceClient(HttpClient http, IMemoryCache cache, ILogger<AuthServiceClient> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"user-contact:{userId}";
        if (_cache.TryGetValue(cacheKey, out UserContact? cached))
            return cached;

        _logger.LogDebug("UserContact: cache miss for user {UserId} — querying auth-service", userId);

        var path = $"/api/users/{userId}/contact";
        try
        {
            var response = await _http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AuthClient: GET {Path} returned {StatusCode}", path, (int)response.StatusCode);
                return null;
            }

            var contact = await response.Content.ReadFromJsonAsync<UserContactDto>(cancellationToken: ct);
            if (contact is null)
            {
                _logger.LogWarning("UserContact: user {UserId} not found in auth-service", userId);
                return null;
            }

            var result = new UserContact(contact.Email, contact.DisplayName ?? contact.Email);
            _cache.Set(cacheKey, result, CacheTtl);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AuthClient: request to {Path} failed", path);
            return null;
        }
    }

    private sealed record UserContactDto(string Email, string? DisplayName);
}
