using System.Net.Http.Json;
using System.Text.Json;
using GraNAS.Notifications.Models;
using GraNAS.Notifications.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GraNAS.Notifications.Services.Implementations;

public class AuthServiceUserSettingsResolver : IUserSettingsResolver
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthServiceUserSettingsResolver> _logger;

    public AuthServiceUserSettingsResolver(HttpClient http, IMemoryCache cache, ILogger<AuthServiceUserSettingsResolver> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<NotificationPrefs> GetPrefsAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"user-prefs:{userId}";
        if (_cache.TryGetValue(cacheKey, out NotificationPrefs? cached) && cached is not null)
            return cached;

        _logger.LogDebug("UserSettingsResolver: cache miss for user {UserId}", userId);

        var path = $"/api/internal/users/{userId}/settings";
        try
        {
            var response = await _http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("UserSettingsResolver: GET {Path} returned {StatusCode} — using defaults", path, (int)response.StatusCode);
                return NotificationPrefs.AllEnabled();
            }

            var dto = await response.Content.ReadFromJsonAsync<SettingsDto>(cancellationToken: ct);
            var prefs = dto?.NotificationPrefs ?? NotificationPrefs.AllEnabled();
            _cache.Set(cacheKey, prefs, CacheTtl);
            return prefs;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "UserSettingsResolver: request to {Path} failed — using defaults", path);
            return NotificationPrefs.AllEnabled();
        }
    }

    private sealed class SettingsDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("notificationPrefs")]
        public NotificationPrefs? NotificationPrefs { get; set; }
    }
}
