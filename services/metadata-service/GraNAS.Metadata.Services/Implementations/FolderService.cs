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
  private readonly IPermissionRepository _permissionRepository;
  private readonly IPermissionService _permissionService;

  public FolderService(
    IFolderRepository folderRepository,
    IPermissionRepository permissionRepository,
    IPermissionService permissionService)
  {
    _folderRepository = folderRepository;
    _permissionRepository = permissionRepository;
    _permissionService = permissionService;
  }

  public async Task<IEnumerable<FolderResponse>> GetUserFoldersAsync(Guid userId)
  {
    var ownedFolders = await _folderRepository.GetUserFoldersAsync(userId);
    var owned = ownedFolders.Select(f => ToResponse(f, AccessLevel.Full, f.OwnerId, null));

    var permissions = await _permissionRepository.ListByUserAsync(userId);
    var shared = permissions
      .Where(p => p.Folder is not null && p.Folder.OwnerId != userId) // exclude if user is also owner
      .Select(p => ToResponse(p.Folder!, p.AccessLevel, p.Folder!.OwnerId, p.Path));

    return owned.Concat(shared).OrderByDescending(f => f.CreatedAt);
  }

  public async Task<CreateFolderResult> CreateFolderAsync(Guid userId, CreateFolderRequest request)
  {
    if (request.ParentFolderId is Guid parentId)
    {
      var hasAccess = await _permissionService.HasAccessAsync(userId, parentId, AccessLevel.Full);
      if (!hasAccess)
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

    return CreateFolderResult.Success(ToResponse(folder, AccessLevel.Full, userId, null));
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

  private static FolderResponse ToResponse(Folder f, AccessLevel accessLevel, Guid ownerId, string? path) =>
    new()
    {
      Id = f.Id,
      Name = f.Name,
      ParentFolderId = f.ParentFolderId,
      OwnerId = ownerId,
      AccessLevel = accessLevel,
      Path = path,
      CreatedAt = f.CreatedAt,
      UpdatedAt = f.UpdatedAt
    };
}
