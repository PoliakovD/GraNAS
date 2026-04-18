using System;
using System.Threading.Tasks;
using GraNAS.Auth.Models;
using GraNAS.Auth.Models.DTO;

namespace GraNAS.Auth.Services.Interfaces;

public interface ITokenService
{
  string GenerateAccessToken(User user);
  Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId);
  Task<TokenResponse> GenerateTokensAsync(User user);
  Task<TokenResponse?> RefreshTokensAsync(string refreshToken);
  Task<bool> RevokeRefreshTokenAsync(string refreshToken, Guid userId);
  Task RevokeAllUserRefreshTokensAsync(Guid userId);
}
