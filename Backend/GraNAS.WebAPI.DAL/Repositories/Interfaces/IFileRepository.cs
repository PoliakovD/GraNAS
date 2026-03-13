using GraNAS.Models;
using File = GraNAS.Models.File;

namespace GraNAS.WebAPI.DAL.Repositories.Interfaces;

public interface IFileRepository
{
  Task<IEnumerable<File>> GetFilesInFolderAsync(Guid folderId);
  Task<File?> GetByIdAsync(Guid id);
  Task CreateAsync(File file);
  Task DeleteAsync(Guid id);
  Task<bool> ExistsAsync(Guid id);
}
