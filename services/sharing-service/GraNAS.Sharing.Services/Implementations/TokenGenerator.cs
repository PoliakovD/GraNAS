using System.Security.Cryptography;
using System.Text;
using GraNAS.Sharing.Services.Interfaces;

namespace GraNAS.Sharing.Services.Implementations;

public class TokenGenerator : ITokenGenerator
{
    public string GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string ComputeHash(string token)
    {
        var inputBytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
