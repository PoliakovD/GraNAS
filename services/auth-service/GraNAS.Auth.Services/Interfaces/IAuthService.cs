using System;
using System.Threading.Tasks;
using GraNAS.Auth.Models.DTO;

namespace GraNAS.Auth.Services.Interfaces;

public enum RegisterError
{
  None,
  WeakPassword,
  EmailAlreadyExists
}

public enum LogoutError
{
  None,
  InvalidToken,
  MissingParameters
}

public record RegisterResult(RegisterError Error, RegisterResponse? Response);
public record LogoutResult(LogoutError Error, string? Message);

public interface IAuthService
{
  Task<RegisterResult> RegisterAsync(RegisterRequest request);
  Task<TokenResponse?> LoginAsync(LoginRequest request);
  Task<TokenResponse?> RefreshAsync(string refreshToken);
  Task<LogoutResult> LogoutAsync(Guid userId, LogoutRequest request);
}
