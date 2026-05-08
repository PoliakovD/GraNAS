using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using GraNAS.Auth.Models.DTO;
using GraNAS.Shared.Models.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GraNAS.WebAPI.Tests.Integration;

public class InternalUsersControllerTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;
    private readonly HttpClient _anonClient;

    public InternalUsersControllerTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
        _anonClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private HttpClient AuthorizedClient()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwt());
        return client;
    }

    private string GenerateJwt()
    {
        var jwt = _factory.Services.GetService(typeof(IConfiguration)) as IConfiguration
                  ?? throw new InvalidOperationException("IConfiguration not available");
        var section = jwt.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section["Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: section["Issuer"],
            audience: section["Audience"],
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Register a user and return their email + userId
    private async Task<(string Email, Guid UserId)> RegisterUserAsync()
    {
        var email = $"internal_{Guid.NewGuid():N}@test.com";
        var resp = await _anonClient.PostAsJsonAsync("/api/Auth/register",
            new { email, password = "Password1!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
        return (email, body!.UserId);
    }

    [Fact]
    public async Task GetByEmail_ExistingUser_Returns200WithIdAndEmail()
    {
        var (email, userId) = await RegisterUserAsync();
        var client = AuthorizedClient();

        var resp = await client.GetAsync($"/api/internal/users/by-email/{Uri.EscapeDataString(email)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<UserLookupResponse>();
        Assert.NotNull(body);
        Assert.Equal(userId, body!.Id);
        Assert.Equal(email, body.Email);
    }

    [Fact]
    public async Task GetByEmail_UnknownEmail_Returns404()
    {
        var client = AuthorizedClient();

        var resp = await client.GetAsync("/api/internal/users/by-email/nobody%40nowhere.com");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("user_not_found", err!.Error);
    }

    [Fact]
    public async Task GetByEmail_NoJwt_Returns401()
    {
        var resp = await _anonClient.GetAsync("/api/internal/users/by-email/any%40test.com");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ──────────────── GET /api/internal/users/batch ────────────────

    [Fact]
    public async Task GetBatch_KnownIds_Returns200WithEmailArray()
    {
        var (email1, userId1) = await RegisterUserAsync();
        var (email2, userId2) = await RegisterUserAsync();
        var client = AuthorizedClient();

        var resp = await client.GetAsync($"/api/internal/users/batch?ids={userId1}&ids={userId2}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<UserLookupResponse[]>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Length);
        Assert.Contains(body, u => u.Id == userId1 && u.Email == email1);
        Assert.Contains(body, u => u.Id == userId2 && u.Email == email2);
    }

    [Fact]
    public async Task GetBatch_UnknownIds_Returns200WithEmptyArray()
    {
        var client = AuthorizedClient();
        var resp = await client.GetAsync($"/api/internal/users/batch?ids={Guid.NewGuid()}&ids={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<UserLookupResponse[]>();
        Assert.NotNull(body);
        Assert.Empty(body!);
    }

    [Fact]
    public async Task GetBatch_EmptyIds_Returns200WithEmptyArray()
    {
        var client = AuthorizedClient();
        var resp = await client.GetAsync("/api/internal/users/batch");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<UserLookupResponse[]>();
        Assert.NotNull(body);
        Assert.Empty(body!);
    }

    [Fact]
    public async Task GetBatch_NoJwt_Returns401()
    {
        var resp = await _anonClient.GetAsync($"/api/internal/users/batch?ids={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
