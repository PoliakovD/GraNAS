using GraNAS.Models;

namespace GraNAS.Metadata.DAL.Repositories.Interfaces;

public interface IFolderRepository
{
  Task<IEnumerable<Folder>> GetUserFoldersAsync(Guid userId);
  Task<Folder?> GetByIdAsync(Guid id);
  Task CreateAsync(Folder folder);
  Task DeleteAsync(Guid id);
  Task<int> GetFilesCountAsync(Guid folderId);
}
