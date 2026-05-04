using GraNAS.Desktop.Contracts.Auth;

namespace GraNAS.Desktop.App.Services.Api;

public interface IAuthApi
{
  Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
  Task RegisterAsync(RegisterRequest request, CancellationToken ct = default);
  Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default);
  Task LogoutAsync(string? refreshToken, CancellationToken ct = default);
  Task<MeResponse?> MeAsync(CancellationToken ct = default);
}
