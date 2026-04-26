using System;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Models.Repositories;
using GraNAS.Metadata.Services.Interfaces;

namespace GraNAS.Metadata.Services.Implementations;

public class PermissionService : IPermissionService
{
  private readonly IFolderRepository _folders;
  private readonly IPermissionRepository _permissions;
  private readonly IAuthServiceClient _authClient;

  public PermissionService(
    IFolderRepository folders,
    IPermissionRepository permissions,
    IAuthServiceClient authClient)
  {
    _folders = folders;
    _permissions = permissions;
    _authClient = authClient;
  }

  public async Task<GrantPermissionResult> GrantAsync(
    Guid ownerId, Guid folderId, GrantPermissionRequest req, CancellationToken ct = default)
  {
    var folder = await _folders.GetByIdForOwnerAsync(folderId, ownerId);
    if (folder is null)
      return GrantPermissionResult.FolderNotFoundOrForbidden();

    var user = await _authClient.GetUserByEmailAsync(req.Email, ct);
    if (user is null)
      return GrantPermissionResult.UserNotFound();

    // Self-grant: owner already has full access, no-op
    if (user.Id == ownerId)
      return GrantPermissionResult.Success(new PermissionResponse
      {
        UserId = ownerId,
        AccessLevel = AccessLevel.Full,
        Path = null,
        CreatedAt = DateTime.UtcNow
      });

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
      return new RevokePermissionResult(RevokePermissionError.FolderNotFoundOrForbidden);

    var deleted = await _permissions.DeleteAsync(folderId, targetUserId);
    return new RevokePermissionResult(deleted ? RevokePermissionError.None : RevokePermissionError.PermissionNotFound);
  }

  public async Task<IReadOnlyList<PermissionResponse>?> ListByFolderAsync(
    Guid ownerId, Guid folderId, CancellationToken ct = default)
  {
    var folder = await _folders.GetByIdForOwnerAsync(folderId, ownerId);
    if (folder is null)
      return null;

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
      return false;

    return permission.AccessLevel >= required;
  }
}
