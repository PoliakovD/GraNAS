using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Models.Repositories;
using GraNAS.Metadata.Services.Interfaces;
using GraNAS.Shared.Messaging.Abstractions;
using GraNAS.Shared.Messaging.Events;
using Microsoft.Extensions.Logging;

namespace GraNAS.Metadata.Services.Implementations;

public class FolderService : IFolderService
{
  private readonly IFolderRepository _folderRepository;
  private readonly IPermissionRepository _permissionRepository;
  private readonly IPermissionService _permissionService;
  private readonly IEventPublisher _eventPublisher;
  private readonly ILogger<FolderService> _logger;

  public FolderService(
    IFolderRepository folderRepository,
    IPermissionRepository permissionRepository,
    IPermissionService permissionService,
    IEventPublisher eventPublisher,
    ILogger<FolderService> logger)
  {
    _folderRepository = folderRepository;
    _permissionRepository = permissionRepository;
    _permissionService = permissionService;
    _eventPublisher = eventPublisher;
    _logger = logger;
  }

  public async Task<IEnumerable<FolderResponse>> GetUserFoldersAsync(Guid userId)
  {
    var ownedFolders = await _folderRepository.GetUserFoldersAsync(userId);
    var owned = ownedFolders.Select(f => ToResponse(f, AccessLevel.Full, f.OwnerId, null));

    var permissions = await _permissionRepository.ListByUserAsync(userId);
    var shared = permissions
      .Where(p => p.Folder is not null && p.Folder.OwnerId != userId)
      .Select(p => ToResponse(p.Folder!, p.AccessLevel, p.Folder!.OwnerId, p.Path));

    var result = owned.Concat(shared).OrderByDescending(f => f.CreatedAt).ToList();
    _logger.LogDebug("GetUserFolders: returning {Count} folders for user {UserId}", result.Count, userId);
    return result;
  }

  public async Task<CreateFolderResult> CreateFolderAsync(Guid userId, CreateFolderRequest request)
  {
    if (request.ParentFolderId is Guid parentId)
    {
      var hasAccess = await _permissionService.HasAccessAsync(userId, parentId, AccessLevel.Full);
      if (!hasAccess)
      {
        _logger.LogWarning("CreateFolder: parent {ParentFolderId} not accessible by {UserId}", parentId, userId);
        return CreateFolderResult.ParentNotFoundOrForbidden();
      }
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
    _logger.LogInformation("CreateFolder: created {FolderId} (parent={ParentFolderId}) for {UserId}",
      folder.Id, request.ParentFolderId, userId);

    return CreateFolderResult.Success(ToResponse(folder, AccessLevel.Full, userId, null));
  }

  public async Task<DeleteFolderResult> DeleteFolderAsync(Guid userId, Guid folderId)
  {
    var folder = await _folderRepository.GetByIdAsync(folderId);
    if (folder == null)
    {
      _logger.LogWarning("DeleteFolder: folder {FolderId} not found (userId={UserId})", folderId, userId);
      return new DeleteFolderResult(DeleteFolderError.NotFound);
    }

    if (folder.OwnerId != userId)
    {
      _logger.LogWarning("DeleteFolder: folder {FolderId} not owned by {UserId}", folderId, userId);
      return new DeleteFolderResult(DeleteFolderError.Forbidden);
    }

    var affectedUsers = await _permissionRepository.GetUsersForFolderAsync(folderId);

    await _folderRepository.DeleteAsync(folderId);
    _logger.LogInformation("DeleteFolder: deleted {FolderId} for {UserId}", folderId, userId);

    foreach (var targetUserId in affectedUsers)
    {
      try
      {
        await _eventPublisher.PublishAsync(new AccessLostEvent
        {
          TargetUserId = targetUserId,
          OwnerId = userId,
          FolderId = folderId,
          FolderName = folder.Name,
          Reason = "folder_deleted"
        });
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "DeleteFolder: failed to publish access_lost event for user {TargetUserId} folder {FolderId}",
          targetUserId, folderId);
      }
    }

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
