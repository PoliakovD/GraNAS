using GraNAS.Desktop.Contracts.Auth;

namespace GraNAS.Desktop.App.Services.Auth;

public interface IAuthSession
{
  string? AccessToken { get; }
  bool IsAuthenticated { get; }
  Guid CurrentUserId { get; }
  string CurrentUserEmail { get; }
  bool IsAdmin { get; }

  event EventHandler? SessionExpired;

  Task<bool> TryRestoreAsync(CancellationToken ct = default);
  Task SignInAsync(TokenResponse tokens, CancellationToken ct = default);
  Task SignOutAsync(CancellationToken ct = default);
  Task<bool> RefreshAsync(CancellationToken ct = default);
}
