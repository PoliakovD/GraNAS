using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraNAS.Metadata.Models.Repositories;

public interface IFolderRepository
{
  Task<IEnumerable<Folder>> GetUserFoldersAsync(Guid userId);
  Task<Folder?> GetByIdAsync(Guid id);
  Task<Folder?> GetByIdForOwnerAsync(Guid id, Guid ownerId);
  Task CreateAsync(Folder folder);
  Task DeleteAsync(Guid id);
  Task<bool> TouchAsync(Guid folderId, System.Threading.CancellationToken ct);
}
