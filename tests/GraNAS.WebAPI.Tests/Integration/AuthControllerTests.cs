using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GraNAS.Auth.Models.DTO;
using GraNAS.Shared.Models.DTO;

namespace GraNAS.WebAPI.Tests.Integration;

/// <summary>
/// Интеграционные тесты AuthController против реальной PostgreSQL (Testcontainers).
/// Фабрика создаётся один раз на весь класс; для изоляции каждый тест использует уникальный email.
/// </summary>
public class AuthControllerTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(AuthWebApplicationFactory factory)
    {
        // BaseAddress = https: при UseHttpsRedirection (Test-env) HTTP→HTTPS 307-редирект
        // дропает Authorization-заголовок. Используем HTTPS сразу — редиректа нет.
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    /// Уникальный email, чтобы тесты не мешали друг другу при общей БД.
    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@test.local";

    // ─────────────────────── POST /api/auth/register ───────────────────────

    [Fact]
    public async Task Register_ValidRequest_Returns200WithUserId()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email    = UniqueEmail(),
            password = "ValidPass1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.UserId);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409WithEmailAlreadyExists()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "ValidPass1" });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "ValidPass1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("email_already_exists", body!.Error);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400WithWeakPasswordError()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email    = UniqueEmail(),
            password = "abcdef"    // >= 6 символов, но нет заглавной и нет цифры
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("weak_password", body!.Error);
    }

    [Fact]
    public async Task Register_InvalidEmailFormat_Returns400WithValidationError()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email    = "not-an-email",
            password = "ValidPass1"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("validation_error", body!.Error);
    }

    // ─────────────────────── POST /api/auth/login ──────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "ValidPass1" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "ValidPass1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("access_token").GetString()?.Length  > 0);
        Assert.True(body.GetProperty("refresh_token").GetString()?.Length > 0);
        Assert.True(body.GetProperty("expires_in").GetInt64() > 0);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401WithInvalidGrant()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = UniqueEmail(),   // пользователь не зарегистрирован
            password = "ValidPass1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("invalid_grant", body!.Error);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "ValidPass1" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass2" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidModel_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "not-email",
            password = "x"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─────────────────────── POST /api/auth/refresh ────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithNewTokens()
    {
        var (_, refreshToken) = await RegisterAndLogin();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("access_token").GetString()?.Length > 0);
        Assert.True(body.GetProperty("refresh_token").GetString()?.Length > 0);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401WithInvalidGrant()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = "this_token_does_not_exist"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("invalid_grant", body!.Error);
    }

    [Fact]
    public async Task Refresh_AlreadyUsedToken_Returns401()
    {
        var (_, refreshToken) = await RegisterAndLogin();

        // Первый refresh — должен пройти и инвалидировать старый токен
        await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        // Повторный — уже отозван
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─────────────────────── POST /api/auth/logout ─────────────────────────

    [Fact]
    public async Task Logout_WithoutAuthHeader_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/logout", new { allSessions = true });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_AllSessions_Returns200WithMessage()
    {
        var (accessToken, _) = await RegisterAndLogin();

        var response = await AuthorizedPost("/api/auth/logout", new { allSessions = true }, accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Logout_WithRefreshToken_Returns200()
    {
        var (accessToken, refreshToken) = await RegisterAndLogin();

        var response = await AuthorizedPost("/api/auth/logout", new { refreshToken }, accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Logout_AlreadyRevokedRefreshToken_Returns400WithInvalidToken()
    {
        var (accessToken, refreshToken) = await RegisterAndLogin();

        // Первый logout — успешно отзываем токен
        await AuthorizedPost("/api/auth/logout", new { refreshToken }, accessToken);

        // Второй — токен уже отозван
        var response = await AuthorizedPost("/api/auth/logout", new { refreshToken }, accessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("invalid_token", body!.Error);
    }

    [Fact]
    public async Task Logout_MissingParameters_Returns400WithInvalidRequest()
    {
        var (accessToken, _) = await RegisterAndLogin();

        // Пустое тело: ни refreshToken, ни allSessions
        var response = await AuthorizedPost("/api/auth/logout", new { }, accessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("invalid_request", body!.Error);
    }

    // ─────────────────────── helpers ───────────────────────────────────────

    private async Task<(string AccessToken, string RefreshToken)> RegisterAndLogin()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "ValidPass1" });

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "ValidPass1" });
        var body      = await loginResp.Content.ReadFromJsonAsync<JsonElement>();

        return (
            body.GetProperty("access_token").GetString()!,
            body.GetProperty("refresh_token").GetString()!
        );
    }

    private Task<HttpResponseMessage> AuthorizedPost(string url, object payload, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }
}
