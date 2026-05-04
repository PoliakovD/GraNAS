using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GraNAS.Auth.Models;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Models.Repositories;
using GraNAS.Auth.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GraNAS.Auth.Services.Implementations;

public class AuthService : IAuthService
{
  private readonly IUserRepository _userRepository;
  private readonly IPasswordHasher _passwordHasher;
  private readonly ITokenService _tokenService;
  private readonly ILogger<AuthService> _logger;

  public AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILogger<AuthService> logger)
  {
    _userRepository = userRepository;
    _passwordHasher = passwordHasher;
    _tokenService = tokenService;
    _logger = logger;
  }

  public async Task<RegisterResult> RegisterAsync(RegisterRequest request)
  {
    if (!IsPasswordStrong(request.Password))
    {
      _logger.LogWarning("Register: weak password for {Email}", request.Email);
      return new RegisterResult(RegisterError.WeakPassword, null);
    }

    if (await _userRepository.EmailExistsAsync(request.Email))
    {
      _logger.LogWarning("Register: email {Email} already exists", request.Email);
      return new RegisterResult(RegisterError.EmailAlreadyExists, null);
    }

    var user = new User
    {
      Id = Guid.NewGuid(),
      Email = request.Email,
      PasswordHash = _passwordHasher.HashPassword(request.Password),
      IsAdmin = false,
      CreatedAt = DateTime.UtcNow
    };

    await _userRepository.CreateAsync(user);
    _logger.LogInformation("Register: user created {UserId} for {Email}", user.Id, user.Email);

    return new RegisterResult(RegisterError.None, new RegisterResponse
    {
      UserId = user.Id,
      Message = "Registration successful."
    });
  }

  public async Task<TokenResponse?> LoginAsync(LoginRequest request)
  {
    var user = await _userRepository.GetByEmailAsync(request.Email);
    if (user == null)
    {
      _logger.LogWarning("Login: user {Email} not found", request.Email);
      return null;
    }

    if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
      _logger.LogWarning("Login: invalid password for user {UserId}", user.Id);
      return null;
    }

    var tokens = await _tokenService.GenerateTokensAsync(user);
    _logger.LogDebug("Login: tokens issued for user {UserId}", user.Id);
    return tokens;
  }

  public Task<TokenResponse?> RefreshAsync(string refreshToken)
  {
    return _tokenService.RefreshTokensAsync(refreshToken);
  }

  public async Task<LogoutResult> LogoutAsync(Guid userId, LogoutRequest request)
  {
    if (request.AllSessions == true)
    {
      await _tokenService.RevokeAllUserRefreshTokensAsync(userId);
      _logger.LogInformation("Logout: revoked all sessions for user {UserId}", userId);
      return new LogoutResult(LogoutError.None, "All sessions have been terminated.");
    }

    if (!string.IsNullOrEmpty(request.RefreshToken))
    {
      var revoked = await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, userId);
      if (revoked)
      {
        _logger.LogInformation("Logout: revoked refresh token for user {UserId}", userId);
        return new LogoutResult(LogoutError.None, "Session terminated successfully.");
      }

      _logger.LogWarning("Logout: refresh token not found for user {UserId}", userId);
      return new LogoutResult(LogoutError.InvalidToken, null);
    }

    return new LogoutResult(LogoutError.MissingParameters, null);
  }

  public async Task<MeResponse?> GetMeAsync(Guid userId)
  {
    var user = await _userRepository.GetByIdAsync(userId);
    if (user is null)
    {
      _logger.LogDebug("GetMe: user {UserId} not found", userId);
      return null;
    }

    return new MeResponse { Id = user.Id, Email = user.Email, IsAdmin = user.IsAdmin };
  }

  private static bool IsPasswordStrong(string password)
  {
    if (password.Length < 6)
      return false;

    bool hasUpper = Regex.IsMatch(password, "[A-Z]");
    bool hasLower = Regex.IsMatch(password, "[a-z]");
    bool hasDigit = Regex.IsMatch(password, "[0-9]");

    return hasUpper && hasLower && hasDigit;
  }
}
