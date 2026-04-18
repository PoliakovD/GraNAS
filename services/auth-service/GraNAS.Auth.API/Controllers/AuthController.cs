using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Services.Interfaces;
using GraNAS.Shared.LoggingService;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GraNAS.Auth.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class AuthController : ControllerBase
{
  private readonly IAuthService _authService;
  private readonly ILoggerService _logger;

  public AuthController(IAuthService authService, ILoggerService logger)
  {
    _authService = authService;
    _logger = logger;
  }

  /// <summary>Регистрация нового пользователя</summary>
  [HttpPost("register")]
  [EnableRateLimiting("auth")]
  [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> Register([FromBody] RegisterRequest request)
  {
    if (!ModelState.IsValid)
      return BadRequest(ModelState);

    var result = await _authService.RegisterAsync(request);

    switch (result.Error)
    {
      case RegisterError.WeakPassword:
        return BadRequest(new ErrorResponse
        {
          Error = "weak_password",
          ErrorDescription = "Password must contain at least one uppercase letter, one lowercase letter, and one digit."
        });
      case RegisterError.EmailAlreadyExists:
        return Conflict(new ErrorResponse
        {
          Error = "email_already_exists",
          ErrorDescription = "User with this email already exists."
        });
    }

    await _logger.LogInfo($"New user registered: {request.Email}", userId: result.Response!.UserId.ToString());
    return Ok(result.Response);
  }

  /// <summary>Вход пользователя (получение токенов)</summary>
  [EnableRateLimiting("auth")]
  [HttpPost("login")]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> Login([FromBody] LoginRequest request)
  {
    if (!ModelState.IsValid)
      return BadRequest(ModelState);

    var tokens = await _authService.LoginAsync(request);
    if (tokens == null)
    {
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_grant",
        ErrorDescription = "Invalid email or password."
      });
    }

    await _logger.LogInfo($"User {request.Email} logged in");

    return Ok(new
    {
      access_token = tokens.AccessToken,
      refresh_token = tokens.RefreshToken,
      expires_in = tokens.ExpiresIn,
      token_type = tokens.TokenType
    });
  }

  /// <summary>Обновление access токена</summary>
  [HttpPost("refresh")]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
  {
    if (!ModelState.IsValid)
      return BadRequest(ModelState);

    var tokens = await _authService.RefreshAsync(request.RefreshToken);
    if (tokens == null)
    {
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_grant",
        ErrorDescription = "Invalid refresh token."
      });
    }

    return Ok(new
    {
      access_token = tokens.AccessToken,
      refresh_token = tokens.RefreshToken,
      expires_in = tokens.ExpiresIn,
      token_type = tokens.TokenType
    });
  }

  /// <summary>Выход из системы</summary>
  [Authorize]
  [HttpPost("logout")]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
  {
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_token",
        ErrorDescription = "Invalid user identifier."
      });

    var result = await _authService.LogoutAsync(userId, request);

    return result.Error switch
    {
      LogoutError.None => Ok(new { message = result.Message }),
      LogoutError.InvalidToken => BadRequest(new ErrorResponse
      {
        Error = "invalid_token",
        ErrorDescription = "Refresh token not found or already revoked."
      }),
      LogoutError.MissingParameters => BadRequest(new ErrorResponse
      {
        Error = "invalid_request",
        ErrorDescription = "Either refresh token or all_sessions flag must be provided."
      }),
      _ => BadRequest()
    };
  }
}
