using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Metadata.DAL.Repositories.Implementation;

public class PermissionRepository : IPermissionRepository
{
  private readonly MetadataDbContext _context;

  public PermissionRepository(MetadataDbContext context)
  {
    _context = context;
  }

  public async Task<Permission?> GetAsync(Guid folderId, Guid userId)
  {
    return await _context.Permissions
      .FirstOrDefaultAsync(p => p.FolderId == folderId && p.UserId == userId);
  }

  public async Task<IEnumerable<Permission>> ListByUserAsync(Guid userId)
  {
    return await _context.Permissions
      .Where(p => p.UserId == userId)
      .Include(p => p.Folder)
      .ToListAsync();
  }

  public async Task UpsertAsync(Permission permission)
  {
    var existing = await GetAsync(permission.FolderId, permission.UserId);
    if (existing is null)
    {
      await _context.Permissions.AddAsync(permission);
    }
    else
    {
      existing.AccessLevel = permission.AccessLevel;
      existing.Path = permission.Path;
      existing.UpdatedAt = DateTime.UtcNow;
      _context.Permissions.Update(existing);
    }

    await _context.SaveChangesAsync();
  }

  public async Task<bool> DeleteAsync(Guid folderId, Guid userId)
  {
    var permission = await GetAsync(folderId, userId);
    if (permission is null)
      return false;

    _context.Permissions.Remove(permission);
    await _context.SaveChangesAsync();
    return true;
  }
}
