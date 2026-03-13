using GraNAS.Models;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.WebAPI.DAL.Repositories.Implementation;

public class FolderRepository : IFolderRepository
{
  private readonly AppDbContext _context;

  public FolderRepository(AppDbContext context)
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

  public async Task<IEnumerable<Folder>> GetChildFoldersAsync(Guid parentId)
  {
    return await _context.Folders
      .Where(f => f.ParentId == parentId)
      .OrderBy(f => f.Name)
      .ToListAsync();
  }

  public async Task<bool> HasSubfoldersAsync(Guid folderId)
  {
    return await _context.Folders.AnyAsync(f => f.ParentId == folderId);
  }
  public async Task<int> GetSubfoldersCountAsync(Guid folderId)
  {
    return await _context.Folders.CountAsync(f => f.ParentId == folderId);
  }

  public async Task<int> GetFilesCountAsync(Guid folderId)
  {
    return await _context.Files.CountAsync(f => f.FolderId == folderId);
  }
}
