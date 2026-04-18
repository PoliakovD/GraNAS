using System;
using System.Threading.Tasks;
using GraNAS.Auth.Models;
using GraNAS.Auth.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Auth.DAL.Repositories.Implementation;

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

  public async Task<RefreshToken?> GetValidTokenAsync(string token)
  {
    var now = DateTime.UtcNow;
    return await _context.RefreshTokens
      .Include(rt => rt.User)
      .FirstOrDefaultAsync(rt => rt.Token == token
                                 && rt.Expires > now
                                 && rt.Revoked == null);
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

  public async Task<bool> RevokeTokenAsync(string token, Guid userId)
  {
    var refreshToken = await _context.RefreshTokens
      .FirstOrDefaultAsync(rt => rt.Token == token && rt.UserId == userId);

    if (refreshToken == null || refreshToken.Revoked != null)
      return false;

    refreshToken.Revoked = DateTime.UtcNow;
    await _context.SaveChangesAsync();
    return true;
  }

  public async Task RevokeAllForUserAsync(Guid userId)
  {
    var now = DateTime.UtcNow;
    var tokens = await _context.RefreshTokens
      .Where(rt => rt.UserId == userId && rt.Revoked == null)
      .ToListAsync();

    foreach (var token in tokens)
    {
      token.Revoked = now;
    }
    await _context.SaveChangesAsync();
  }
}
