using System.Security.Cryptography;
using System.Text;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GraNAS.Signaling.Services.Implementations;

public class TurnCredentialService : ITurnCredentialService
{
    private readonly byte[] _secret;
    private readonly string[] _uris;
    private readonly int _ttl;

    public TurnCredentialService(IConfiguration config)
    {
        var secret = config["Turn:StaticAuthSecret"]
            ?? throw new InvalidOperationException("Turn:StaticAuthSecret is not configured");
        _secret = Encoding.UTF8.GetBytes(secret);
        _uris = config.GetSection("Turn:Uris").Get<string[]>()
            ?? throw new InvalidOperationException("Turn:Uris is not configured");
        _ttl = int.TryParse(config["Turn:TtlSeconds"], out var v) ? v : 600;
    }

    public TurnCredentials Generate(string userId)
    {
        var expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _ttl;
        var username = $"{expiry}:{userId}";
        var credential = Convert.ToBase64String(HMACSHA1.HashData(_secret, Encoding.UTF8.GetBytes(username)));
        return new TurnCredentials(username, credential, _uris, _ttl);
    }
}
