using System.Threading.Tasks;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Models.Repositories;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GraNAS.Auth.API.Controllers;

[Authorize]
[ApiController]
[Route("api/internal/users")]
[Produces("application/json")]
public class InternalUsersController : ControllerBase
{
  private readonly IUserRepository _users;

  public InternalUsersController(IUserRepository users) => _users = users;

  /// <summary>Поиск пользователя по email (межсервисный вызов из metadata-service)</summary>
  [HttpGet("by-email/{email}")]
  [ProducesResponseType(typeof(UserLookupResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> GetByEmail(string email)
  {
    var user = await _users.GetByEmailAsync(email);
    if (user is null)
      return NotFound(new ErrorResponse
      {
        Error = "user_not_found",
        ErrorDescription = $"User with email '{email}' not found."
      });

    return Ok(new UserLookupResponse { Id = user.Id, Email = user.Email });
  }
}
