using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace GraNAS.WebAPI.Tests.Integration;

public class AvatarControllerTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;

    public AvatarControllerTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client() => _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    private static string UniqueEmail() => $"avatar-{Guid.NewGuid():N}@test.local";

    private async Task<HttpClient> RegisterAndLogin()
    {
        var email = UniqueEmail();
        var password = "ValidPass1";
        var client = Client();

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("access_token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static MultipartFormDataContent PngContent(int sizeBytes = 100)
    {
        var bytes = new byte[sizeBytes];
        // minimal PNG header
        bytes[0] = 0x89; bytes[1] = 0x50; bytes[2] = 0x4E; bytes[3] = 0x47;
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "avatar.png");
        return form;
    }

    // ─────────────────────── POST /api/auth/me/avatar ───────────────────────

    [Fact]
    public async Task PostAvatar_ValidPng_Returns204()
    {
        var client = await RegisterAndLogin();
        var resp = await client.PostAsync("/api/auth/me/avatar", PngContent());
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task PostAvatar_TooLarge_Returns400()
    {
        var client = await RegisterAndLogin();
        var resp = await client.PostAsync("/api/auth/me/avatar", PngContent(257 * 1024));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PostAvatar_WrongContentType_Returns400()
    {
        var client = await RegisterAndLogin();

        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("notanimage"));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(content, "file", "bad.txt");

        var resp = await client.PostAsync("/api/auth/me/avatar", form);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ─────────────────────── GET /api/auth/me/avatar ────────────────────────

    [Fact]
    public async Task GetAvatar_NoAvatar_Returns404()
    {
        var client = await RegisterAndLogin();
        var resp = await client.GetAsync("/api/auth/me/avatar");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetAvatar_AfterUpload_Returns200WithBytes()
    {
        var client = await RegisterAndLogin();
        await client.PostAsync("/api/auth/me/avatar", PngContent(200));

        var resp = await client.GetAsync("/api/auth/me/avatar");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("image/png", resp.Content.Headers.ContentType?.MediaType);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.Equal(200, bytes.Length);
    }

    // ─────────────────────── DELETE /api/auth/me/avatar ─────────────────────

    [Fact]
    public async Task DeleteAvatar_Returns204_ThenGetReturns404()
    {
        var client = await RegisterAndLogin();
        await client.PostAsync("/api/auth/me/avatar", PngContent());

        var del = await client.DeleteAsync("/api/auth/me/avatar");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await client.GetAsync("/api/auth/me/avatar");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
