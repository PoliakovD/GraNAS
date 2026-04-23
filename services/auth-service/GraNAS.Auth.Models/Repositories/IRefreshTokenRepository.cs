using System;
using System.Threading.Tasks;

namespace GraNAS.Auth.Models.Repositories;

public interface IRefreshTokenRepository
{
  Task AddAsync(RefreshToken refreshToken);
  Task<RefreshToken?> GetByTokenAsync(string token);
  Task<RefreshToken?> GetValidTokenAsync(string token);
  Task RevokeAsync(Guid id);
  Task<bool> RevokeTokenAsync(string token, Guid userId);
  Task RevokeAllForUserAsync(Guid userId);
}
