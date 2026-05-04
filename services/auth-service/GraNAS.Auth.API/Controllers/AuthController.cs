using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Services.Interfaces;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GraNAS.Auth.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class AuthController : ControllerBase
{
  private const string RefreshTokenCookieName = "refresh_token";
  private const string RefreshTokenCookiePath = "/api/auth";

  private readonly IAuthService _authService;
  private readonly IWebHostEnvironment _env;
  private readonly ILogger<AuthController> _logger;

  public AuthController(IAuthService authService, IWebHostEnvironment env, ILogger<AuthController> logger)
  {
    _authService = authService;
    _env = env;
    _logger = logger;
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
        _logger.LogWarning("Registration rejected for {Email}: weak password", request.Email);
        return BadRequest(new ErrorResponse
        {
          Error = "weak_password",
          ErrorDescription = "Password must contain at least one uppercase letter, one lowercase letter, and one digit."
        });
      case RegisterError.EmailAlreadyExists:
        _logger.LogWarning("Registration rejected for {Email}: email already exists", request.Email);
        return Conflict(new ErrorResponse
        {
          Error = "email_already_exists",
          ErrorDescription = "User with this email already exists."
        });
    }

    _logger.LogInformation("User registered: {Email} (id={UserId})", request.Email, result.Response!.UserId);
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
      _logger.LogWarning("Login failed for {Email}: invalid credentials", request.Email);
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_grant",
        ErrorDescription = "Invalid email or password."
      });
    }

    _logger.LogInformation("User logged in: {Email}", request.Email);

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
    {
      _logger.LogWarning("Refresh failed: missing refresh token");
      return BadRequest(new ErrorResponse
      {
        Error = "invalid_request",
        ErrorDescription = "Refresh token is required."
      });
    }

    var tokens = await _authService.RefreshAsync(refreshToken);
    if (tokens == null)
    {
      _logger.LogWarning("Refresh failed: invalid or expired refresh token");
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_grant",
        ErrorDescription = "Invalid refresh token."
      });
    }

    _logger.LogDebug("Refresh tokens issued");

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
    {
      _logger.LogWarning("/me failed: invalid user identifier in token");
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_token",
        ErrorDescription = "Invalid user identifier."
      });
    }

    var me = await _authService.GetMeAsync(userId);
    if (me is null)
    {
      _logger.LogWarning("/me failed: user {UserId} not found", userId);
      return Unauthorized(new ErrorResponse
      {
        Error = "user_not_found",
        ErrorDescription = "User not found."
      });
    }

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
    {
      _logger.LogWarning("Logout failed: invalid user identifier in token");
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_token",
        ErrorDescription = "Invalid user identifier."
      });
    }

    request ??= new LogoutRequest();

    // cookie takes priority; supplement body if RefreshToken not provided
    var cookieToken = Request.Cookies[RefreshTokenCookieName];
    if (cookieToken != null && string.IsNullOrEmpty(request.RefreshToken))
      request.RefreshToken = cookieToken;

    var result = await _authService.LogoutAsync(userId, request);

    if (result.Error == LogoutError.None)
    {
      DeleteRefreshTokenCookie();
      _logger.LogInformation("User {UserId} logged out (allSessions={AllSessions})", userId, request.AllSessions == true);
      return Ok(new { message = result.Message });
    }

    if (result.Error == LogoutError.InvalidToken)
    {
      _logger.LogWarning("Logout failed: refresh token not found or revoked (userId={UserId})", userId);
      return BadRequest(new ErrorResponse
      {
        Error = "invalid_token",
        ErrorDescription = "Refresh token not found or already revoked."
      });
    }

    if (result.Error == LogoutError.MissingParameters)
    {
      _logger.LogWarning("Logout failed: missing parameters (userId={UserId})", userId);
      return BadRequest(new ErrorResponse
      {
        Error = "invalid_request",
        ErrorDescription = "Either refresh token or all_sessions flag must be provided."
      });
    }

    return BadRequest();
  }
}
