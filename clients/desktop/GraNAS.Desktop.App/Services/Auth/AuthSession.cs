using GraNAS.Desktop.Contracts.Auth;
using ReactiveUI;

namespace GraNAS.Desktop.App.Services.Auth;

public class AuthSession : ReactiveObject, IAuthSession
{
  private const string RefreshTokenKey = "RefreshToken";

  private readonly ICredentialStore _store;
  private readonly Func<string, Task<TokenResponse?>> _refreshFunc;
  private readonly Func<Task<MeResponse?>> _meFunc;

  private string? _accessToken;
  private bool _isAuthenticated;
  private Guid _currentUserId;
  private string _currentUserEmail = string.Empty;
  private bool _isAdmin;

  private readonly SemaphoreSlim _refreshLock = new(1, 1);
  private Task<bool>? _inflightRefresh;

  public AuthSession(
    ICredentialStore store,
    Func<string, Task<TokenResponse?>> refreshFunc,
    Func<Task<MeResponse?>> meFunc)
  {
    _store = store;
    _refreshFunc = refreshFunc;
    _meFunc = meFunc;
  }

  public string? AccessToken => _accessToken;

  public bool IsAuthenticated
  {
    get => _isAuthenticated;
    private set => this.RaiseAndSetIfChanged(ref _isAuthenticated, value);
  }

  public Guid CurrentUserId
  {
    get => _currentUserId;
    private set => this.RaiseAndSetIfChanged(ref _currentUserId, value);
  }

  public string CurrentUserEmail
  {
    get => _currentUserEmail;
    private set => this.RaiseAndSetIfChanged(ref _currentUserEmail, value);
  }

  public bool IsAdmin
  {
    get => _isAdmin;
    private set => this.RaiseAndSetIfChanged(ref _isAdmin, value);
  }

  public event EventHandler? SessionExpired;

  public async Task<bool> TryRestoreAsync(CancellationToken ct = default)
  {
    var stored = _store.Get(RefreshTokenKey);
    if (string.IsNullOrEmpty(stored))
      return false;

    var tokens = await _refreshFunc(stored);
    if (tokens is null)
    {
      _store.Delete(RefreshTokenKey);
      return false;
    }

    await ApplyTokensAsync(tokens, ct);
    return true;
  }

  public async Task SignInAsync(TokenResponse tokens, CancellationToken ct = default)
  {
    await ApplyTokensAsync(tokens, ct);
  }

  public async Task SignOutAsync(CancellationToken ct = default)
  {
    _accessToken = null;
    _store.Delete(RefreshTokenKey);
    IsAuthenticated = false;
    CurrentUserId = Guid.Empty;
    CurrentUserEmail = string.Empty;
    IsAdmin = false;
    await Task.CompletedTask;
  }

  public Task<bool> RefreshAsync(CancellationToken ct = default)
  {
    // Deduplicate inflight refresh (port of client.ts _refreshPromise)
    if (_inflightRefresh is { IsCompleted: false })
      return _inflightRefresh;

    _inflightRefresh = DoRefreshAsync(ct);
    return _inflightRefresh;
  }

  private async Task<bool> DoRefreshAsync(CancellationToken ct)
  {
    await _refreshLock.WaitAsync(ct);
    try
    {
      var stored = _store.Get(RefreshTokenKey);
      if (string.IsNullOrEmpty(stored))
      {
        RaiseSessionExpired();
        return false;
      }

      var tokens = await _refreshFunc(stored);
      if (tokens is null)
      {
        _store.Delete(RefreshTokenKey);
        RaiseSessionExpired();
        return false;
      }

      await ApplyTokensAsync(tokens, ct);
      return true;
    }
    finally
    {
      _refreshLock.Release();
    }
  }

  private async Task ApplyTokensAsync(TokenResponse tokens, CancellationToken ct)
  {
    _accessToken = tokens.AccessToken;
    _store.Save(RefreshTokenKey, tokens.RefreshToken);

    // Resolve current user via /me endpoint if available
    try
    {
      var me = await _meFunc();
      if (me is not null)
      {
        CurrentUserId = me.Id;
        CurrentUserEmail = me.Email;
        IsAdmin = me.IsAdmin;
      }
      else
      {
        // Fallback: decode from JWT
        var (userId, email, isAdmin) = JwtTokenReader.Read(tokens.AccessToken);
        CurrentUserId = userId;
        CurrentUserEmail = email;
        IsAdmin = isAdmin;
      }
    }
    catch
    {
      var (userId, email, isAdmin) = JwtTokenReader.Read(tokens.AccessToken);
      CurrentUserId = userId;
      CurrentUserEmail = email;
      IsAdmin = isAdmin;
    }

    IsAuthenticated = true;
  }

  private void RaiseSessionExpired()
  {
    IsAuthenticated = false;
    SessionExpired?.Invoke(this, EventArgs.Empty);
  }
}
