using System;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GraNAS.Models;
using GraNAS.Models.DTO;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using GraNAS.WebAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace GraNAS.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
  private readonly IUserRepository _userRepository;
  private readonly IPasswordHasher _passwordHasher;
  private readonly ITokenService _tokenService;

  public AuthController(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
  {
    _userRepository = userRepository;
    _passwordHasher = passwordHasher;
    _tokenService = tokenService;
  }

  [HttpPost("register")]
  public async Task<IActionResult> Register([FromBody] RegisterRequest request)
  {
    // 1. Базовая валидация модели (атрибуты)
    if (!ModelState.IsValid)
    {
      return BadRequest(ModelState);
    }

    // 2. Дополнительная валидация сложности пароля (можно расширить)
    if (!IsPasswordStrong(request.Password))
    {
      ModelState.AddModelError("Password",
        "Password must contain at least one uppercase letter, one lowercase letter, and one digit.");
      return BadRequest(ModelState);
    }

    // 3. Проверка уникальности email
    var emailExists = await _userRepository.EmailExistsAsync(request.Email);
    if (emailExists)
    {
      return Conflict(new
      {
        error = "email_already_exists",
        error_description = "User with this email already exists."
      });
    }

    // 4. Хеширование пароля
    var passwordHash = _passwordHasher.HashPassword(request.Password);

    // 5. Создание пользователя
    var user = new User
    {
      Id = Guid.NewGuid(),
      Email = request.Email,
      PasswordHash = passwordHash,
      IsAdmin = false,
      CreatedAt = DateTime.UtcNow
    };

    await _userRepository.CreateAsync(user);

    // 6. Возвращаем успешный ответ (без токенов, только подтверждение регистрации)
    var response = new RegisterResponse
    {
      UserId = user.Id,
      Message = "Registration successful."
    };

    return Ok(response);
  }

  [HttpPost("login")]
  public async Task<IActionResult> Login([FromBody] LoginRequest request)
  {
    // 1. Валидация модели
    if (!ModelState.IsValid)
    {
      return BadRequest(ModelState);
    }

    // 2. Поиск пользователя по email
    var user = await _userRepository.GetByEmailAsync(request.Email);
    if (user == null)
    {
      // Общее сообщение об ошибке (не уточняем, что именно не так)
      return Unauthorized(new
      {
        error = "invalid_grant",
        error_description = "Invalid email or password."
      });
    }

    // 3. Проверка пароля
    if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
      return Unauthorized(new
      {
        error = "invalid_grant",
        error_description = "Invalid email or password."
      });
    }

    // 4. Генерация токенов
    var tokens = await _tokenService.GenerateTokensAsync(user);

    // 5. Возврат успешного ответа
    return Ok(new
    {
      access_token = tokens.AccessToken,
      refresh_token = tokens.RefreshToken,
      expires_in = tokens.ExpiresIn,
      token_type = tokens.TokenType,
      // опционально можно вернуть id пользователя
      user_id = user.Id
    });
  }

  [HttpPost("refresh")]
  public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
  {
    if (!ModelState.IsValid)
      return BadRequest(ModelState);

    var tokens = await _tokenService.RefreshTokensAsync(request.RefreshToken);
    if (tokens == null)
    {
      return Unauthorized(new
      {
        error = "invalid_grant",
        error_description = "Invalid refresh token."
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

  [Authorize]
  [HttpPost("logout")]
  public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
  {
    // Получаем ID текущего пользователя из claims
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
      return Unauthorized(new { error = "invalid_token", error_description = "Invalid user identifier." });

    // Если запрошен выход из всех сессий
    if (request.AllSessions == true)
    {
      await _tokenService.RevokeAllUserRefreshTokensAsync(userId);
      return Ok(new { message = "All sessions have been terminated." });
    }

    // Если передан конкретный refresh token
    if (!string.IsNullOrEmpty(request.RefreshToken))
    {
      var revoked = await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, userId);
      if (!revoked)
        return BadRequest(new { error = "invalid_token", error_description = "Refresh token not found or already revoked." });

      return Ok(new { message = "Session terminated successfully." });
    }

    // Если ничего не указано
    return BadRequest(new { error = "invalid_request", error_description = "Either refresh token or all_sessions flag must be provided." });
  }

  private bool IsPasswordStrong(string password)
  {
    // Минимальные требования: длина >= 6, хотя бы одна заглавная, одна строчная, одна цифра
    if (password.Length < 6)
      return false;

    bool hasUpper = Regex.IsMatch(password, "[A-Z]");
    bool hasLower = Regex.IsMatch(password, "[a-z]");
    bool hasDigit = Regex.IsMatch(password, "[0-9]");

    return hasUpper && hasLower && hasDigit;
  }
}
