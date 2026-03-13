using GraNAS.Models;

namespace GraNAS.WebAPI.DAL.Repositories.Interfaces;

public interface IFolderRepository
{
  Task<IEnumerable<Folder>> GetUserFoldersAsync(Guid userId);
  Task<Folder?> GetByIdAsync(Guid id);
  Task CreateAsync(Folder folder);
  Task DeleteAsync(Guid id);
  Task<int> GetFilesCountAsync(Guid folderId);
  Task<IEnumerable<Folder>> GetChildFoldersAsync(Guid parentId);
  Task<bool> HasSubfoldersAsync(Guid folderId);
  Task<int> GetSubfoldersCountAsync(Guid folderId);
}
