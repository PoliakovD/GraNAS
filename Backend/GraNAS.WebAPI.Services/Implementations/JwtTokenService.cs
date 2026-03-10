using GraNAS.Models;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using GraNAS.WebAPI.Services.Interfaces;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GraNAS.WebAPI.Services.Implementations;

public class JwtTokenService : ITokenService
{
  private readonly IConfiguration _configuration;
  private readonly IRefreshTokenRepository _refreshTokenRepository;

  public JwtTokenService(IConfiguration configuration, IRefreshTokenRepository refreshTokenRepository)
  {
    _configuration = configuration;
    _refreshTokenRepository = refreshTokenRepository;
  }

  public string GenerateAccessToken(User user)
  {
    var jwtSettings = _configuration.GetSection("Jwt");
    var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);
    var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
      new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
      new Claim(JwtRegisteredClaimNames.Email, user.Email),
      new Claim("is_admin", user.IsAdmin.ToString())
      // можно добавить другие claims
    };

    var token = new JwtSecurityToken(
      issuer: jwtSettings["Issuer"],
      audience: jwtSettings["Audience"],
      claims: claims,
      expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["AccessTokenExpirationMinutes"])),
      signingCredentials: signingCredentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  public async Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId)
  {
    // Генерация криптостойкого токена (32 байта -> Base64)
    var randomBytes = new byte[32];
    using (var rng = RandomNumberGenerator.Create())
    {
      rng.GetBytes(randomBytes);
    }

    var refreshTokenString = Convert.ToBase64String(randomBytes);

    var refreshToken = new RefreshToken
    {
      Id = Guid.NewGuid(),
      UserId = userId,
      Token = refreshTokenString,
      Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["Jwt:RefreshTokenExpirationDays"])),
      CreatedAt = DateTime.UtcNow,
      Revoked = null
    };

    // Сохраняем в БД (репозиторий должен обработать уникальность Token)
    // Если возникнет конфликт уникальности (очень редко), можно повторить генерацию.
    // Для простоты считаем, что вставка пройдёт успешно.
    // TODO обернуть в try catch
    await _refreshTokenRepository.AddAsync(refreshToken);

    return refreshToken;
  }

  public async Task<TokenResponse> GenerateTokensAsync(User user)
  {
    var accessToken = GenerateAccessToken(user);
    var refreshToken = await GenerateRefreshTokenAsync(user.Id);

    var expiresIn = Convert.ToInt64(_configuration["Jwt:AccessTokenExpirationMinutes"]) * 60; // в секундах

    return new TokenResponse
    {
      AccessToken = accessToken,
      RefreshToken = refreshToken.Token,
      ExpiresIn = expiresIn
    };
  }
}
