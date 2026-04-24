using System;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Models.DTO;

namespace GraNAS.Metadata.Services.Interfaces;

public interface IPermissionService
{
  Task<GrantPermissionResult> GrantAsync(Guid ownerId, Guid folderId, GrantPermissionRequest req, CancellationToken ct = default);
  Task<RevokePermissionResult> RevokeAsync(Guid ownerId, Guid folderId, Guid targetUserId);
  Task<bool> HasAccessAsync(Guid userId, Guid folderId, AccessLevel required);
}
