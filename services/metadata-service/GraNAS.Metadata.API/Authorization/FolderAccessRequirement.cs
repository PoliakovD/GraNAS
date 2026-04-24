using GraNAS.Metadata.Models;
using Microsoft.AspNetCore.Authorization;

namespace GraNAS.Metadata.API.Authorization;

public record FolderAccessRequirement(AccessLevel Required) : IAuthorizationRequirement;
