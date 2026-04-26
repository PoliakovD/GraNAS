namespace GraNAS.Metadata.Models.Repositories;

public interface IPermissionRepository
{
  Task<Permission?> GetAsync(Guid folderId, Guid userId);
  Task<IEnumerable<Permission>> ListByUserAsync(Guid userId);
  Task<IEnumerable<Permission>> ListByFolderAsync(Guid folderId);
  Task UpsertAsync(Permission permission);
  Task<bool> DeleteAsync(Guid folderId, Guid userId);
}
