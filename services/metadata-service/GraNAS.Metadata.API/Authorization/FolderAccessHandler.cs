using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Metadata.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace GraNAS.Metadata.API.Authorization;

public class FolderAccessHandler : AuthorizationHandler<FolderAccessRequirement, Guid>
{
  private readonly IPermissionService _permissions;

  public FolderAccessHandler(IPermissionService permissions)
  {
    _permissions = permissions;
  }

  protected override async Task HandleRequirementAsync(
    AuthorizationHandlerContext context,
    FolderAccessRequirement requirement,
    Guid folderId)
  {
    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
      return;

    if (await _permissions.HasAccessAsync(userId, folderId, requirement.Required))
      context.Succeed(requirement);
  }
}
