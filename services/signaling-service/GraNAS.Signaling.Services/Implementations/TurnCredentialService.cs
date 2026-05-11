using System.Security.Cryptography;
using System.Text;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GraNAS.Signaling.Services.Implementations;

/// <summary>
/// Генератор краткосрочных TURN-учётных данных по RFC 8489 (Long-Term Credentials Mechanism).
/// Использует общий секрет (<c>Turn:StaticAuthSecret</c>), синхронизированный с конфигурацией coturn.
/// </summary>
public class TurnCredentialService : ITurnCredentialService
{
    private readonly byte[] _secret;
    private readonly string[] _uris;
    private readonly int _ttl;

    /// <summary>
    /// Инициализирует сервис, читая конфигурацию TURN из <see cref="IConfiguration"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Если <c>Turn:StaticAuthSecret</c> или <c>Turn:Uris</c> не заданы.</exception>
    public TurnCredentialService(IConfiguration config)
    {
        var secret = config["Turn:StaticAuthSecret"]
            ?? throw new InvalidOperationException("Turn:StaticAuthSecret is not configured");
        _secret = Encoding.UTF8.GetBytes(secret);
        _uris = config.GetSection("Turn:Uris").Get<string[]>()
            ?? throw new InvalidOperationException("Turn:Uris is not configured");
        _ttl = int.TryParse(config["Turn:TtlSeconds"], out var v) ? v : 600;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Алгоритм: <c>expiry = now + ttl</c>; <c>username = "{expiry}:{userId}"</c>;
    /// <c>credential = Base64(HMAC-SHA1(secret, username))</c>.
    /// Учётные данные действительны до наступления <c>expiry</c> (Unix timestamp).
    /// </remarks>
    public TurnCredentials Generate(string userId)
    {
        var expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _ttl;
        var username = $"{expiry}:{userId}";
        var credential = Convert.ToBase64String(HMACSHA1.HashData(_secret, Encoding.UTF8.GetBytes(username)));
        return new TurnCredentials(username, credential, _uris, _ttl);
    }
}
