using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Models.Repositories;
using GraNAS.Metadata.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GraNAS.Metadata.Services.Implementations;

public class PermissionService : IPermissionService
{
  private readonly IFolderRepository _folders;
  private readonly IPermissionRepository _permissions;
  private readonly IAuthServiceClient _authClient;
  private readonly ILogger<PermissionService> _logger;

  public PermissionService(
    IFolderRepository folders,
    IPermissionRepository permissions,
    IAuthServiceClient authClient,
    ILogger<PermissionService> logger)
  {
    _folders = folders;
    _permissions = permissions;
    _authClient = authClient;
    _logger = logger;
  }

  public async Task<GrantPermissionResult> GrantAsync(
    Guid ownerId, Guid folderId, GrantPermissionRequest req, CancellationToken ct = default)
  {
    var folder = await _folders.GetByIdForOwnerAsync(folderId, ownerId);
    if (folder is null)
    {
      _logger.LogWarning("Grant: folder {FolderId} not owned by {OwnerId}", folderId, ownerId);
      return GrantPermissionResult.FolderNotFoundOrForbidden();
    }

    var user = await _authClient.GetUserByEmailAsync(req.Email, ct);
    if (user is null)
    {
      _logger.LogWarning("Grant: target user with email {Email} not found (folder={FolderId})", req.Email, folderId);
      return GrantPermissionResult.UserNotFound();
    }

    // Self-grant: owner already has full access, no-op
    if (user.Id == ownerId)
    {
      _logger.LogWarning("Grant: refused self-grant on folder {FolderId} by {OwnerId}", folderId, ownerId);
      return GrantPermissionResult.Success(new PermissionResponse
      {
        UserId = ownerId,
        AccessLevel = AccessLevel.Full,
        Path = null,
        CreatedAt = DateTime.UtcNow
      });
    }

    var permission = new Permission
    {
      Id = Guid.NewGuid(),
      FolderId = folderId,
      UserId = user.Id,
      AccessLevel = req.AccessLevel,
      Path = req.Path,
      CreatedAt = DateTime.UtcNow
    };

    await _permissions.UpsertAsync(permission);
    _logger.LogInformation("Grant: permission {AccessLevel} on folder {FolderId} to user {TargetUserId} by {OwnerId}",
      req.AccessLevel, folderId, user.Id, ownerId);

    return GrantPermissionResult.Success(new PermissionResponse
    {
      UserId = user.Id,
      Email = user.Email,
      AccessLevel = req.AccessLevel,
      Path = req.Path,
      CreatedAt = permission.CreatedAt
    });
  }

  public async Task<RevokePermissionResult> RevokeAsync(Guid ownerId, Guid folderId, Guid targetUserId)
  {
    var folder = await _folders.GetByIdForOwnerAsync(folderId, ownerId);
    if (folder is null)
    {
      _logger.LogWarning("Revoke: folder {FolderId} not owned by {OwnerId}", folderId, ownerId);
      return new RevokePermissionResult(RevokePermissionError.FolderNotFoundOrForbidden);
    }

    var deleted = await _permissions.DeleteAsync(folderId, targetUserId);
    if (!deleted)
    {
      _logger.LogWarning("Revoke: no existing permission on folder {FolderId} for user {TargetUserId}", folderId, targetUserId);
      return new RevokePermissionResult(RevokePermissionError.PermissionNotFound);
    }

    _logger.LogInformation("Revoke: removed permission on folder {FolderId} from user {TargetUserId} by {OwnerId}",
      folderId, targetUserId, ownerId);
    return new RevokePermissionResult(RevokePermissionError.None);
  }

  public async Task<IReadOnlyList<PermissionResponse>?> ListByFolderAsync(
    Guid ownerId, Guid folderId, CancellationToken ct = default)
  {
    var folder = await _folders.GetByIdForOwnerAsync(folderId, ownerId);
    if (folder is null)
    {
      _logger.LogWarning("List: folder {FolderId} not owned by {OwnerId}", folderId, ownerId);
      return null;
    }

    var permissions = await _permissions.ListByFolderAsync(folderId);

    var result = new List<PermissionResponse>();
    foreach (var p in permissions)
    {
      var user = await _authClient.GetUserByIdAsync(p.UserId, ct);
      result.Add(new PermissionResponse
      {
        UserId = p.UserId,
        Email = user?.Email,
        AccessLevel = p.AccessLevel,
        Path = p.Path,
        CreatedAt = p.CreatedAt
      });
    }

    return result;
  }

  public async Task<bool> HasAccessAsync(Guid userId, Guid folderId, AccessLevel required)
  {
    // Owner always has Full access
    var ownedFolder = await _folders.GetByIdForOwnerAsync(folderId, userId);
    if (ownedFolder is not null)
      return true;

    var permission = await _permissions.GetAsync(folderId, userId);
    if (permission is null)
    {
      _logger.LogDebug("HasAccess: user {UserId} has no access to folder {FolderId} (required={Required})",
        userId, folderId, required);
      return false;
    }

    var hasAccess = permission.AccessLevel >= required;
    if (!hasAccess)
      _logger.LogDebug("HasAccess: user {UserId} folder {FolderId} has {Actual} but required {Required}",
        userId, folderId, permission.AccessLevel, required);
    return hasAccess;
  }
}
