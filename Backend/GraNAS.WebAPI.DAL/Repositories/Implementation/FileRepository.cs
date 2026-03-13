using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using GraNAS.Models;
using File = GraNAS.Models.File;

namespace GraNAS.WebAPI.DAL.Repositories.Implementation;

public class FileRepository : IFileRepository
{
  private readonly AppDbContext _context;

  public FileRepository(AppDbContext context)
  {
    _context = context;
  }

  public async Task<IEnumerable<File>> GetFilesInFolderAsync(Guid folderId)
  {
    return await _context.Files
      .Where(f => f.FolderId == folderId)
      .OrderByDescending(f => f.CreatedAt)
      .ToListAsync();
  }

  public async Task<File?> GetByIdAsync(Guid id)
  {
    return await _context.Files.FindAsync(id);
  }

  public async Task CreateAsync(File file)
  {
    await _context.Files.AddAsync(file);
    await _context.SaveChangesAsync();
  }

  public async Task DeleteAsync(Guid id)
  {
    var file = await _context.Files.FindAsync(id);
    if (file != null)
    {
      _context.Files.Remove(file);
      await _context.SaveChangesAsync();
    }
  }

  public async Task<bool> ExistsAsync(Guid id)
  {
    return await _context.Files.AnyAsync(f => f.Id == id);
  }
}
