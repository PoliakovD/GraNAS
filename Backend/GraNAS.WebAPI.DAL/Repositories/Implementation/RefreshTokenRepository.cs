using GraNAS.Models;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.WebAPI.DAL.Repositories.Implementation;

public class RefreshTokenRepository : IRefreshTokenRepository
{
  private readonly AppDbContext _context;

  public RefreshTokenRepository(AppDbContext context)
  {
    _context = context;
  }

  public async Task AddAsync(RefreshToken refreshToken)
  {
    await _context.RefreshTokens.AddAsync(refreshToken);
    await _context.SaveChangesAsync();
  }

  public async Task<RefreshToken?> GetByTokenAsync(string token)
  {
    return await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token);
  }

  public async Task RevokeAsync(Guid id)
  {
    var token = await _context.RefreshTokens.FindAsync(id);
    if (token != null)
    {
      token.Revoked = DateTime.UtcNow;
      await _context.SaveChangesAsync();
    }
  }
}
