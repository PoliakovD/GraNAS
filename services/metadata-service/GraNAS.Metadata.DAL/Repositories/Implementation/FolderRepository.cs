using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Metadata.DAL.Repositories.Implementation;

public class FolderRepository : IFolderRepository
{
  private readonly MetadataDbContext _context;

  public FolderRepository(MetadataDbContext context)
  {
    _context = context;
  }

  public async Task<IEnumerable<Folder>> GetUserFoldersAsync(Guid userId)
  {
    return await _context.Folders
      .Where(f => f.OwnerId == userId)
      .OrderByDescending(f => f.CreatedAt)
      .ToListAsync();
  }

  public async Task<Folder?> GetByIdAsync(Guid id)
  {
    return await _context.Folders.FindAsync(id);
  }

  public async Task<Folder?> GetByIdForOwnerAsync(Guid id, Guid ownerId)
  {
    return await _context.Folders
      .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId);
  }

  public async Task CreateAsync(Folder folder)
  {
    await _context.Folders.AddAsync(folder);
    await _context.SaveChangesAsync();
  }

  public async Task DeleteAsync(Guid id)
  {
    var folder = await _context.Folders.FindAsync(id);
    if (folder != null)
    {
      _context.Folders.Remove(folder);
      await _context.SaveChangesAsync();
    }
  }

  public async Task<bool> TouchAsync(Guid folderId, CancellationToken ct)
  {
    var folder = await _context.Folders.FindAsync([folderId], ct);
    if (folder is null) return false;
    folder.LastAccessedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync(ct);
    return true;
  }
}
