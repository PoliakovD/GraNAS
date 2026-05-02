using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GraNAS.Auth.Models;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Models.Repositories;
using GraNAS.Auth.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GraNAS.Auth.Services.Implementations;

public class JwtTokenService : ITokenService
{
  private readonly IConfiguration _configuration;
  private readonly IRefreshTokenRepository _refreshTokenRepository;
  private readonly ILogger<JwtTokenService> _logger;

  public JwtTokenService(
    IConfiguration configuration,
    IRefreshTokenRepository refreshTokenRepository,
    ILogger<JwtTokenService> logger)
  {
    _configuration = configuration;
    _refreshTokenRepository = refreshTokenRepository;
    _logger = logger;
  }

  public string GenerateAccessToken(User user)
  {
    var jwtSettings = _configuration.GetSection("Jwt");
    var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
    var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
      new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
      new Claim(JwtRegisteredClaimNames.Email, user.Email),
      new Claim("is_admin", user.IsAdmin.ToString())
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

    await _refreshTokenRepository.AddAsync(refreshToken);
    return refreshToken;
  }

  public async Task<TokenResponse> GenerateTokensAsync(User user)
  {
    var accessToken = GenerateAccessToken(user);
    var refreshToken = await GenerateRefreshTokenAsync(user.Id);

    var expiresIn = Convert.ToInt64(_configuration["Jwt:AccessTokenExpirationMinutes"]) * 60;

    return new TokenResponse
    {
      AccessToken = accessToken,
      RefreshToken = refreshToken.Token,
      ExpiresIn = expiresIn
    };
  }

  public async Task<TokenResponse?> RefreshTokensAsync(string refreshToken)
  {
    var existingToken = await _refreshTokenRepository.GetValidTokenAsync(refreshToken);
    if (existingToken == null)
    {
      _logger.LogWarning("JWT: refresh token not found, expired, or revoked");
      return null;
    }

    await _refreshTokenRepository.RevokeAsync(existingToken.Id);

    var user = existingToken.User;
    if (user == null)
    {
      _logger.LogWarning("JWT: refresh token {TokenId} has no associated user", existingToken.Id);
      return null;
    }

    _logger.LogDebug("JWT: tokens rotated for user {UserId}", user.Id);
    return await GenerateTokensAsync(user);
  }

  public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, Guid userId)
  {
    var revoked = await _refreshTokenRepository.RevokeTokenAsync(refreshToken, userId);
    if (!revoked)
      _logger.LogWarning("JWT: revoke refused — token not found or not owned by user {UserId}", userId);
    return revoked;
  }

  public Task RevokeAllUserRefreshTokensAsync(Guid userId)
  {
    _logger.LogInformation("JWT: revoking all refresh tokens for user {UserId}", userId);
    return _refreshTokenRepository.RevokeAllForUserAsync(userId);
  }
}
