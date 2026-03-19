using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GraNAS.Models.DTO;
using GraNAS.WebAPI.Tests;
using Xunit;
using FluentAssertions;
using GraNAS.WebAPI.DAL;
using Xunit.Abstractions;

namespace GraNAS.WebAPI.IntegrationTests;

[Collection("Sequential")]
public class AuthControllerIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>
{
  private readonly HttpClient _client;
  private readonly TestWebApplicationFactory<Program> _factory;
  private readonly ITestOutputHelper _testOutputHelper;
  private readonly AppDbContext _context;

  public AuthControllerIntegrationTests(TestWebApplicationFactory<Program> factory, ITestOutputHelper testOutputHelper)
  {
    // _factory = factory;
    // _testOutputHelper = testOutputHelper;
    // _client = factory.CreateClient();
    //
    // _context = factory.CreateContext();
    // _context.Database.EnsureCreated();
    //
    //
    _factory = factory;
    _testOutputHelper = testOutputHelper;
    _context = factory.CreateContext();

    // Очистка БД
    _context.Database.EnsureDeleted();
    _context.Database.EnsureCreated();

    // Создаём HttpClient без редиректов и с возможностью игнорировать SSL

     _client = factory.CreateClient();

  }

  [Fact]
  public async Task Register_ValidRequest_ReturnsOkAndCreatesUser()
  {
    // Arrange
    var request = new RegisterRequest
    {
      Email = $"testuser_{Guid.NewGuid()}@test.com",
      Password = "StrongP@ss1"
    };

    // Act
    var response = await _client.PostAsJsonAsync("/api/auth/register", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
    result.Should().NotBeNull();
    result!.UserId.Should().NotBe(Guid.Empty);
  }

  [Fact]
  public async Task Login_ValidCredentials_ReturnsTokens()
  {
    // Arrange: Сначала зарегистрируем пользователя
    var email = $"loginuser_{Guid.NewGuid()}@test.com";
    var password = "StrongP@ss1";

    var registerRequest = new RegisterRequest { Email = email, Password = password };
    await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

    var loginRequest = new LoginRequest { Email = email, Password = password };

    // Act
    var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var tokens = await response.Content.ReadFromJsonAsync<JsonElement>();
    tokens.TryGetProperty("access_token", out _).Should().BeTrue();
    tokens.TryGetProperty("refresh_token", out _).Should().BeTrue();
    tokens.TryGetProperty("expires_in", out _).Should().BeTrue();
  }

  [Fact]
  public async Task Refresh_WithValidToken_ReturnsNewTokens()
  {
    // Arrange: Регистрация + Логин
    var email = $"refreshuser_{Guid.NewGuid()}@test.com";
    var password = "StrongP@ss1";

    await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = password });
    var loginResponse =
      await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
    var tokens = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
    var refreshToken = tokens.GetProperty("refresh_token").GetString();
    var accessToken = tokens.GetProperty("access_token").GetString();
    _client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

    var refreshRequest = new { RefreshToken = refreshToken };

    // Act
    var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);


    await Task.Delay(3000);


    var newTokens = await response.Content.ReadFromJsonAsync<JsonElement>();
    newTokens.TryGetProperty("access_token", out var newAccessToken).Should().BeTrue();
    var newAccessTokenStr = newAccessToken.GetString();

    newAccessTokenStr.Should().NotBe(accessToken, "Security Measures");
  }

  [Fact]
  public async Task Logout_WithValidSession_ReturnsOk()
  {
    // Arrange
    var email = $"logoutuser_{Guid.NewGuid()}@test.com";
    var password = "StrongP@ss1";

    // Регистрация
    await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = password });

    // Логин
    var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
    var tokens = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
    var refreshToken = tokens.GetProperty("refresh_token").GetString();
    var accessToken = tokens.GetProperty("access_token").GetString();

    // Убедимся, что токен получен
    accessToken.Should().NotBeNullOrEmpty("Access token должен быть возвращён при логине");

    // Создание запроса с авторизацией в заголовке (внутри request)
    var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
    {
      Content = JsonContent.Create(new { refreshToken = refreshToken })
    };

    // Добавляем заголовок Authorization непосредственно в запрос
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    // Для отладки: выведем, что отправляется
    _testOutputHelper.WriteLine($"Заголовок Authorization: {request.Headers.Authorization}");
    _testOutputHelper.WriteLine($"Тело запроса: {await request.Content.ReadAsStringAsync()}");

    // Act
    var response = await _client.SendAsync(request);
    var responseContent = await response.Content.ReadAsStringAsync();
    await Task.Delay(500);
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK,
      $"Ожидался статус 200 OK, но получен {response.StatusCode}. Тело ответа: {responseContent}");
  }
}
