using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Services.Interfaces;
using GraNAS.Shared.LoggingService;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;

namespace GraNAS.Auth.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class AuthController : ControllerBase
{
  private const string RefreshTokenCookieName = "refresh_token";
  private const string RefreshTokenCookiePath = "/api/auth";

  private readonly IAuthService _authService;
  private readonly ILoggerService _logger;
  private readonly IWebHostEnvironment _env;

  public AuthController(IAuthService authService, ILoggerService logger, IWebHostEnvironment env)
  {
    _authService = authService;
    _logger = logger;
    _env = env;
  }

  private void SetRefreshTokenCookie(string token)
  {
    Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
    {
      HttpOnly = true,
      Secure = !_env.IsDevelopment(),
      SameSite = SameSiteMode.Lax,
      Path = RefreshTokenCookiePath,
      Expires = DateTimeOffset.UtcNow.AddDays(7)
    });
  }

  private void DeleteRefreshTokenCookie()
  {
    Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
    {
      Path = RefreshTokenCookiePath
    });
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

    SetRefreshTokenCookie(tokens.RefreshToken);
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
  [EnableRateLimiting("auth")]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> Refresh(
    [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request)
  {
    var refreshToken = Request.Cookies[RefreshTokenCookieName] ?? request?.RefreshToken;
    if (string.IsNullOrEmpty(refreshToken))
      return BadRequest(new ErrorResponse
      {
        Error = "invalid_request",
        ErrorDescription = "Refresh token is required."
      });

    var tokens = await _authService.RefreshAsync(refreshToken);
    if (tokens == null)
    {
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_grant",
        ErrorDescription = "Invalid refresh token."
      });
    }

    SetRefreshTokenCookie(tokens.RefreshToken);
    return Ok(new
    {
      access_token = tokens.AccessToken,
      refresh_token = tokens.RefreshToken,
      expires_in = tokens.ExpiresIn,
      token_type = tokens.TokenType
    });
  }

  /// <summary>Получить профиль текущего пользователя</summary>
  [Authorize]
  [HttpGet("me")]
  [EnableRateLimiting("auth")]
  [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Me()
  {
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_token",
        ErrorDescription = "Invalid user identifier."
      });

    var me = await _authService.GetMeAsync(userId);
    if (me is null)
      return Unauthorized(new ErrorResponse
      {
        Error = "user_not_found",
        ErrorDescription = "User not found."
      });

    return Ok(me);
  }

  /// <summary>Выход из системы</summary>
  [Authorize]
  [HttpPost("logout")]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Logout(
    [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutRequest? request)
  {
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_token",
        ErrorDescription = "Invalid user identifier."
      });

    request ??= new LogoutRequest();

    // cookie takes priority; supplement body if RefreshToken not provided
    var cookieToken = Request.Cookies[RefreshTokenCookieName];
    if (cookieToken != null && string.IsNullOrEmpty(request.RefreshToken))
      request.RefreshToken = cookieToken;

    var result = await _authService.LogoutAsync(userId, request);

    if (result.Error == LogoutError.None)
      DeleteRefreshTokenCookie();

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
