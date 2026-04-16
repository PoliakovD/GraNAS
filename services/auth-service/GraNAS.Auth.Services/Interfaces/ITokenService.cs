using GraNAS.Models;
using GraNAS.Models.DTO;

namespace GraNAS.Auth.Services.Interfaces;

public interface ITokenService
{
  /// <summary>
  /// Генерирует JWT access token для пользователя
  /// </summary>
  string GenerateAccessToken(User user);

  /// <summary>
  /// Генерирует криптостойкий refresh token и сохраняет его в БД
  /// </summary>
  Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId);

  /// <summary>
  /// Генерирует пару токенов (access + refresh) и сохраняет refresh token в БД
  /// </summary>
  Task<TokenResponse> GenerateTokensAsync(User user);

  Task<TokenResponse?> RefreshTokensAsync(string refreshToken);

  Task<bool> RevokeRefreshTokenAsync(string refreshToken, Guid userId);
  Task RevokeAllUserRefreshTokensAsync(Guid userId);
}
