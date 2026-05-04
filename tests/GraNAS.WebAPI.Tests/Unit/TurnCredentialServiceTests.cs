using System.Security.Cryptography;
using System.Text;
using GraNAS.Signaling.Services.Implementations;
using Microsoft.Extensions.Configuration;

namespace GraNAS.WebAPI.Tests.Unit;

public class TurnCredentialServiceTests
{
    private const string Secret = "test_turn_secret_min_32_chars_long_value_ok";
    private readonly TurnCredentialService _sut;

    public TurnCredentialServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Turn:StaticAuthSecret"] = Secret,
                ["Turn:Uris:0"] = "turn:localhost:3478?transport=udp",
                ["Turn:Uris:1"] = "turn:localhost:3478?transport=tcp",
                ["Turn:TtlSeconds"] = "600",
            })
            .Build();

        _sut = new TurnCredentialService(config);
    }

    [Fact]
    public void Generate_Username_ContainsExpiryAndUserId()
    {
        var userId = "user-abc";
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600 - 2;

        var result = _sut.Generate(userId);

        var parts = result.Username.Split(':');
        Assert.Equal(2, parts.Length);
        Assert.True(long.TryParse(parts[0], out var ts));
        Assert.True(ts >= before);
        Assert.Equal(userId, parts[1]);
    }

    [Fact]
    public void Generate_Credential_IsValidHmacSha1OfUsername()
    {
        var userId = "verify-me";
        var result = _sut.Generate(userId);

        var key = Encoding.UTF8.GetBytes(Secret);
        var data = Encoding.UTF8.GetBytes(result.Username);
        var expected = Convert.ToBase64String(HMACSHA1.HashData(key, data));

        Assert.Equal(expected, result.Credential);
    }

    [Fact]
    public void Generate_ReturnsAllConfiguredUris()
    {
        var result = _sut.Generate("u");
        Assert.Equal(2, result.Uris.Length);
        Assert.Contains("turn:localhost:3478?transport=udp", result.Uris);
        Assert.Contains("turn:localhost:3478?transport=tcp", result.Uris);
    }

    [Fact]
    public void Generate_TtlMatchesConfig()
    {
        var result = _sut.Generate("u");
        Assert.Equal(600, result.Ttl);
    }

    [Fact]
    public void Generate_DifferentUsers_HaveDifferentCredentials()
    {
        var r1 = _sut.Generate("user-1");
        var r2 = _sut.Generate("user-2");
        Assert.NotEqual(r1.Credential, r2.Credential);
        Assert.NotEqual(r1.Username, r2.Username);
    }
}
