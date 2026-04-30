using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GraNAS.Signaling.Models.DTO;

namespace GraNAS.WebAPI.Tests.Integration;

public class TurnControllerTests : IClassFixture<SignalingWebApplicationFactory>
{
    private readonly SignalingWebApplicationFactory _factory;

    public TurnControllerTests(SignalingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AuthClient(Guid? userId = null)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.GenerateJwt(userId ?? Guid.NewGuid()));
        return client;
    }

    private HttpClient AnonClient() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    [Fact]
    public async Task GetCredentials_Authenticated_Returns200WithValidStructure()
    {
        var client = AuthClient();

        var response = await client.GetAsync("/api/turn/credentials");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TurnCredentialsResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Username);
        Assert.NotEmpty(body.Credential);
        Assert.NotEmpty(body.Uris);
        Assert.True(body.Ttl > 0);
    }

    [Fact]
    public async Task GetCredentials_Anonymous_Returns401()
    {
        var response = await AnonClient().GetAsync("/api/turn/credentials");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCredentials_UsernameContainsUserIdAndExpiry()
    {
        var userId = Guid.NewGuid();
        var client = AuthClient(userId);
        var beforeTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600 - 5;

        var response = await client.GetAsync("/api/turn/credentials");

        var body = await response.Content.ReadFromJsonAsync<TurnCredentialsResponse>();
        var parts = body!.Username.Split(':');
        Assert.Equal(2, parts.Length);
        Assert.True(long.TryParse(parts[0], out var ts));
        Assert.True(ts >= beforeTs, $"Expected ts >= {beforeTs}, got {ts}");
        Assert.Equal(userId.ToString(), parts[1]);
    }

    [Fact]
    public async Task GetCredentials_CredentialIsValidHmacSha1()
    {
        var userId = Guid.NewGuid();
        var client = AuthClient(userId);

        var response = await client.GetAsync("/api/turn/credentials");
        var body = await response.Content.ReadFromJsonAsync<TurnCredentialsResponse>();

        // Re-derive expected HMAC using the same secret from appsettings.Test.json
        const string secret = "test_turn_secret_min_32_chars_long_value_ok";
        var key = Encoding.UTF8.GetBytes(secret);
        var expected = Convert.ToBase64String(HMACSHA1.HashData(key, Encoding.UTF8.GetBytes(body!.Username)));

        Assert.Equal(expected, body.Credential);
    }

    [Fact]
    public async Task GetCredentials_TwoRequestsSameUser_ProduceDifferentTokensOverTime()
    {
        var userId = Guid.NewGuid();
        var client = AuthClient(userId);

        var r1 = await (await client.GetAsync("/api/turn/credentials")).Content
            .ReadFromJsonAsync<TurnCredentialsResponse>();
        await Task.Delay(1100); // ensure timestamp differs
        var r2 = await (await client.GetAsync("/api/turn/credentials")).Content
            .ReadFromJsonAsync<TurnCredentialsResponse>();

        // After >1 second, the expiry timestamp should differ, producing different username+credential
        Assert.NotEqual(r1!.Username, r2!.Username);
    }
}
