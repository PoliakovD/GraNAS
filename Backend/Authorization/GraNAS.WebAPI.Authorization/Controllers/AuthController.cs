using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GraNAS.Models;
using GraNAS.Models.DTO;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using GraNAS.WebAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GraNAS.WebAPI.Authorization.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class AuthController : ControllerBase
{
  private readonly IUserRepository _userRepository;
  private readonly IPasswordHasher _passwordHasher;
  private readonly ITokenService _tokenService;
  private readonly ILoggerService _logger;


  public AuthController(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILoggerService logger)
  {
    _userRepository = userRepository;
    _passwordHasher = passwordHasher;
    _tokenService = tokenService;
    _logger = logger;
  }

  /// <summary>
  /// Регистрация нового пользователя
  /// </summary>
  /// <param name="request">Данные для регистрации (email, пароль)</param>
  /// <returns>Информация о созданном пользователе</returns>
  /// <response code="200">Пользователь успешно зарегистрирован</response>
  /// <response code="400">Ошибка валидации (некорректный email или пароль)</response>
  /// <response code="409">Пользователь с таким email уже существует</response>
  /// <response code="429">Слишком много запросов (превышен лимит rate limiting)</response>
  [HttpPost("register")]
  [EnableRateLimiting("auth")] // если используете именованную политику
  [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
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
      return Conflict(new ErrorResponse
      {
        Error = "email_already_exists",
        ErrorDescription = "User with this email already exists."
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

    await _logger.LogInfo($"New user registered: {request.Email}", userId: user.Id.ToString());

    return Ok(response);
  }

  /// <summary>
  /// Вход пользователя (получение токенов)
  /// </summary>
  /// <param name="request">Учётные данные (email, пароль)</param>
  /// <returns>Access и refresh токены</returns>
  /// <response code="200">Успешная аутентификация, возвращены токены</response>
  /// <response code="400">Ошибка валидации запроса</response>
  /// <response code="401">Неверный email или пароль</response>
  /// <response code="429">Слишком много запросов</response>

  [EnableRateLimiting("auth")]
  [HttpPost("login")]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)] // можно уточнить тип, но он анонимный
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
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
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_grant",
        ErrorDescription = "Invalid email or password."
      });
    }

    // 3. Проверка пароля
    if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_grant",
        ErrorDescription = "Invalid email or password."
      });
    }

    // 4. Генерация токенов
    var tokens = await _tokenService.GenerateTokensAsync(user);

    await _logger.LogInfo($"User {user.Email} logged in", userId: user.Id.ToString());

    // 5. Возврат успешного ответа
    return Ok(new
    {
      access_token = tokens.AccessToken,
      refresh_token = tokens.RefreshToken,
      expires_in = tokens.ExpiresIn,
      token_type = tokens.TokenType,
      user_id = user.Id
    });
  }

  /// <summary>
  /// Обновление access токена с использованием refresh токена
  /// </summary>
  /// <param name="request">Refresh токен</param>
  /// <returns>Новая пара токенов</returns>
  /// <response code="200">Токены успешно обновлены</response>
  /// <response code="400">Невалидный запрос</response>
  /// <response code="401">Недействительный refresh токен</response>
  [HttpPost("refresh")]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
  {
    if (!ModelState.IsValid)
      return BadRequest(ModelState);

    var tokens = await _tokenService.RefreshTokensAsync(request.RefreshToken);
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

  /// <summary>
  /// Выход из системы (отзыв refresh токена)
  /// </summary>
  /// <param name="request">Refresh токен для отзыва или флаг завершения всех сессий</param>
  /// <returns>Сообщение о результате</returns>
  /// <response code="200">Сессия(и) завершены</response>
  /// <response code="400">Не указан токен или неверный запрос</response>
  /// <response code="401">Недействительный access токен или пользователь не идентифицирован</response>
  [Authorize]
  [HttpPost("logout")]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)] // анонимный объект с message
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
  {
    // Получаем ID текущего пользователя из claims
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
      return Unauthorized(new ErrorResponse
      {
        Error = "invalid_token",
        ErrorDescription = "Invalid user identifier."
      });

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
        return BadRequest(new ErrorResponse
        {
          Error = "invalid_token",
          ErrorDescription = "Refresh token not found or already revoked."
        });

      return Ok(new { message = "Session terminated successfully." });
    }

    // Если ничего не указано
    return BadRequest(new ErrorResponse
    {
      Error = "invalid_request",
      ErrorDescription = "Either refresh token or all_sessions flag must be provided."
    });
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
