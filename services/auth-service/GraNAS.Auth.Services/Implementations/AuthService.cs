using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GraNAS.Auth.Models;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Models.Repositories;
using GraNAS.Auth.Services.Interfaces;

namespace GraNAS.Auth.Services.Implementations;

public class AuthService : IAuthService
{
  private readonly IUserRepository _userRepository;
  private readonly IPasswordHasher _passwordHasher;
  private readonly ITokenService _tokenService;

  public AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
  {
    _userRepository = userRepository;
    _passwordHasher = passwordHasher;
    _tokenService = tokenService;
  }

  public async Task<RegisterResult> RegisterAsync(RegisterRequest request)
  {
    if (!IsPasswordStrong(request.Password))
      return new RegisterResult(RegisterError.WeakPassword, null);

    if (await _userRepository.EmailExistsAsync(request.Email))
      return new RegisterResult(RegisterError.EmailAlreadyExists, null);

    var user = new User
    {
      Id = Guid.NewGuid(),
      Email = request.Email,
      PasswordHash = _passwordHasher.HashPassword(request.Password),
      IsAdmin = false,
      CreatedAt = DateTime.UtcNow
    };

    await _userRepository.CreateAsync(user);

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
      return null;

    if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
      return null;

    return await _tokenService.GenerateTokensAsync(user);
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
      return new LogoutResult(LogoutError.None, "All sessions have been terminated.");
    }

    if (!string.IsNullOrEmpty(request.RefreshToken))
    {
      var revoked = await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, userId);
      return revoked
        ? new LogoutResult(LogoutError.None, "Session terminated successfully.")
        : new LogoutResult(LogoutError.InvalidToken, null);
    }

    return new LogoutResult(LogoutError.MissingParameters, null);
  }

  public async Task<MeResponse?> GetMeAsync(Guid userId)
  {
    var user = await _userRepository.GetByIdAsync(userId);
    if (user is null)
      return null;

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
