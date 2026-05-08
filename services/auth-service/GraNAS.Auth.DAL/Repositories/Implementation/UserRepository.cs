using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Auth.Models;
using GraNAS.Auth.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Auth.DAL.Repositories.Implementation;

public class UserRepository : IUserRepository
{
  private readonly AppDbContext _context;

  public UserRepository(AppDbContext context)
  {
    _context = context;
  }

  public async Task<User?> GetByEmailAsync(string email)
  {
    return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
  }

  public async Task<User?> GetByIdAsync(Guid id)
  {
    return await _context.Users.FindAsync(id);
  }

  public async Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
  {
    var distinct = ids.Distinct().Take(200).ToArray();
    if (distinct.Length == 0) return Array.Empty<User>();
    return await _context.Users.Where(u => distinct.Contains(u.Id)).ToListAsync(ct);
  }

  public async Task CreateAsync(User user)
  {
    await _context.Users.AddAsync(user);
    await _context.SaveChangesAsync();
  }

  public async Task<bool> EmailExistsAsync(string email)
  {
    return await _context.Users.AnyAsync(u => u.Email == email);
  }
}
