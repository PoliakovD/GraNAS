using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Models.Repositories;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GraNAS.Auth.API.Controllers;

[Authorize]
[ApiController]
[Route("api/internal/users")]
[Produces("application/json")]
public class InternalUsersController : ControllerBase
{
  private readonly IUserRepository _users;
  private readonly ILogger<InternalUsersController> _logger;

  public InternalUsersController(IUserRepository users, ILogger<InternalUsersController> logger)
  {
    _users = users;
    _logger = logger;
  }

  /// <summary>Поиск пользователя по id (межсервисный вызов из metadata-service)</summary>
  [HttpGet("{id:guid}")]
  [ProducesResponseType(typeof(UserLookupResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> GetById(Guid id)
  {
    var user = await _users.GetByIdAsync(id);
    if (user is null)
    {
      _logger.LogDebug("Internal lookup: user {UserId} not found", id);
      return NotFound(new ErrorResponse
      {
        Error = "user_not_found",
        ErrorDescription = $"User with id '{id}' not found."
      });
    }

    return Ok(new UserLookupResponse { Id = user.Id, Email = user.Email });
  }

  /// <summary>Батч-поиск пользователей по id (межсервисный вызов из metadata-service)</summary>
  [HttpGet("batch")]
  [ProducesResponseType(typeof(UserLookupResponse[]), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> GetBatch([FromQuery] Guid[] ids, CancellationToken ct)
  {
    if (ids.Length == 0) return Ok(Array.Empty<UserLookupResponse>());
    var users = await _users.GetByIdsAsync(ids, ct);
    return Ok(users.Select(u => new UserLookupResponse { Id = u.Id, Email = u.Email }));
  }

  /// <summary>Поиск пользователя по email (межсервисный вызов из metadata-service)</summary>
  [HttpGet("by-email/{email}")]
  [ProducesResponseType(typeof(UserLookupResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> GetByEmail(string email)
  {
    var user = await _users.GetByEmailAsync(email);
    if (user is null)
    {
      _logger.LogDebug("Internal lookup: user with email {Email} not found", email);
      return NotFound(new ErrorResponse
      {
        Error = "user_not_found",
        ErrorDescription = $"User with email '{email}' not found."
      });
    }

    return Ok(new UserLookupResponse { Id = user.Id, Email = user.Email });
  }
}
