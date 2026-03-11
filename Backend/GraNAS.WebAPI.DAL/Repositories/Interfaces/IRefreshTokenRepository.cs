using GraNAS.Models;

namespace GraNAS.WebAPI.DAL.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
  Task AddAsync(RefreshToken refreshToken);
  Task<RefreshToken> GetByTokenAsync(string token);
  Task<RefreshToken?> GetValidTokenAsync(string token);
  Task RevokeAsync(Guid id);

}
