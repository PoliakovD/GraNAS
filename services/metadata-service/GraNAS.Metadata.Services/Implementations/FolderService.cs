using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Models.Repositories;
using GraNAS.Metadata.Services.Interfaces;

namespace GraNAS.Metadata.Services.Implementations;

public class FolderService : IFolderService
{
  private readonly IFolderRepository _folderRepository;

  public FolderService(IFolderRepository folderRepository)
  {
    _folderRepository = folderRepository;
  }

  public async Task<IEnumerable<FolderResponse>> GetUserFoldersAsync(Guid userId)
  {
    var folders = await _folderRepository.GetUserFoldersAsync(userId);

    return folders.Select(f => new FolderResponse
    {
      Id = f.Id,
      ParentFolderId = f.ParentFolderId,
      Name = f.Name,
      CreatedAt = f.CreatedAt,
      UpdatedAt = f.UpdatedAt
    });
  }

  public async Task<CreateFolderResult> CreateFolderAsync(Guid userId, CreateFolderRequest request)
  {
    if (request.ParentFolderId is Guid parentId)
    {
      var parent = await _folderRepository.GetByIdForOwnerAsync(parentId, userId);
      if (parent == null)
        return CreateFolderResult.ParentNotFoundOrForbidden();
    }

    var folder = new Folder
    {
      Id = Guid.NewGuid(),
      OwnerId = userId,
      ParentFolderId = request.ParentFolderId,
      Name = request.Name,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = null
    };

    await _folderRepository.CreateAsync(folder);

    return CreateFolderResult.Success(new FolderResponse
    {
      Id = folder.Id,
      ParentFolderId = folder.ParentFolderId,
      Name = folder.Name,
      CreatedAt = folder.CreatedAt,
      UpdatedAt = folder.UpdatedAt
    });
  }

  public async Task<DeleteFolderResult> DeleteFolderAsync(Guid userId, Guid folderId)
  {
    var folder = await _folderRepository.GetByIdAsync(folderId);
    if (folder == null)
      return new DeleteFolderResult(DeleteFolderError.NotFound);

    if (folder.OwnerId != userId)
      return new DeleteFolderResult(DeleteFolderError.Forbidden);

    await _folderRepository.DeleteAsync(folderId);
    return new DeleteFolderResult(DeleteFolderError.None);
  }
}
