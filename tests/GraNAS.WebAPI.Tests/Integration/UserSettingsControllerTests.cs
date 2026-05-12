using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GraNAS.Auth.DAL;
using GraNAS.Auth.Models.DTO;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.WebAPI.Tests.Integration;

public class UserSettingsControllerTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;

    public UserSettingsControllerTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress   = new Uri("https://localhost"),
        HandleCookies = false
    });

    private static string UniqueEmail() => $"settings-{Guid.NewGuid():N}@test.local";

    private async Task<(HttpClient client, string token)> RegisterAndLogin()
    {
        var email    = UniqueEmail();
        var password = "ValidPass1";
        var client   = Client();

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var body  = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("access_token").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    // ─────────────────────── GET /api/auth/me/settings ───────────────────────

    [Fact]
    public async Task GetSettings_FirstCall_ReturnsDefaultsWithEmailOn()
    {
        var (client, _) = await RegisterAndLogin();

        var response = await client.GetAsync("/api/auth/me/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserSettingsResponse>();
        Assert.NotNull(body);
        Assert.True(body!.NotificationPrefs.Email.AccessGranted);
        Assert.True(body.NotificationPrefs.InApp.AccessGranted);
        Assert.False(body.NotificationPrefs.WebPush.AccessGranted);
    }

    [Fact]
    public async Task GetSettings_Unauthenticated_Returns401()
    {
        var client   = Client();
        var response = await client.GetAsync("/api/auth/me/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─────────────────────── PUT /api/auth/me/settings ───────────────────────

    [Fact]
    public async Task PutSettings_UpdatesAndReturnsOnNextGet()
    {
        var (client, _) = await RegisterAndLogin();

        var updated = new
        {
            notificationPrefs = new
            {
                email   = new { access_granted = false, access_revoked = true,  share_revoked = true,  access_lost = true  },
                inApp   = new { access_granted = true,  access_revoked = true,  share_revoked = true,  access_lost = true  },
                webPush = new { access_granted = false, access_revoked = false, share_revoked = false, access_lost = false }
            }
        };

        var put = await client.PutAsJsonAsync("/api/auth/me/settings", updated);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var get  = await client.GetAsync("/api/auth/me/settings");
        var body = await get.Content.ReadFromJsonAsync<UserSettingsResponse>();
        Assert.False(body!.NotificationPrefs.Email.AccessGranted);
        Assert.True(body.NotificationPrefs.Email.AccessRevoked);
    }

    [Fact]
    public async Task PutSettings_Unauthenticated_Returns401()
    {
        var client = Client();
        var put    = await client.PutAsJsonAsync("/api/auth/me/settings", new { notificationPrefs = new { } });
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    // ─────────────────────── Register with consent ───────────────────────────

    [Fact]
    public async Task Register_WithConsentFalse_CreatesSettingsWithEmailOff()
    {
        var email  = UniqueEmail();
        var client = Client();

        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password                   = "ValidPass1",
            emailNotificationsConsent  = false
        });

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "ValidPass1" });
        var body  = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("access_token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var settings = await client.GetAsync("/api/auth/me/settings");
        var s = await settings.Content.ReadFromJsonAsync<UserSettingsResponse>();
        Assert.False(s!.NotificationPrefs.Email.AccessGranted);
        Assert.True(s.NotificationPrefs.InApp.AccessGranted);
    }

    [Fact]
    public async Task Register_WithConsentTrue_CreatesSettingsWithEmailOn()
    {
        var email  = UniqueEmail();
        var client = Client();

        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password                  = "ValidPass1",
            emailNotificationsConsent = true
        });

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "ValidPass1" });
        var body  = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("access_token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var settings = await client.GetAsync("/api/auth/me/settings");
        var s = await settings.Content.ReadFromJsonAsync<UserSettingsResponse>();
        Assert.True(s!.NotificationPrefs.Email.AccessGranted);
    }

    // ─────────── GET /api/internal/users/{id}/settings ───────────────────────

    [Fact]
    public async Task GetInternalSettings_Returns200WithDefaults()
    {
        var (client, _) = await RegisterAndLogin();

        // Получить userId через /api/auth/me
        var me     = await client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>();
        var userId = meBody!.Id;

        var response = await client.GetAsync($"/api/internal/users/{userId}/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserSettingsResponse>();
        Assert.NotNull(body);
        Assert.True(body!.NotificationPrefs.InApp.AccessGranted);
    }
}
