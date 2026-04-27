using GraNAS.Desktop.Contracts.Auth;

namespace GraNAS.Desktop.App.Services.Api;

public class AuthApi : ApiBase, IAuthApi
{
  public AuthApi(HttpClient http) : base(http) { }

  public async Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
  {
    try { return await PostAsync<TokenResponse>("api/auth/login", request, ct); }
    catch (ApiException ex) when (ex.StatusCode == 401) { return null; }
  }

  public Task RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    => PostAsync("api/auth/register", request, ct);

  public async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default)
  {
    try { return await PostAsync<TokenResponse>("api/auth/refresh", new { refreshToken }, ct); }
    catch (ApiException ex) when (ex.StatusCode == 401) { return null; }
  }

  public Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    => PostAsync("api/auth/logout", new { refreshToken }, ct);

  public async Task<MeResponse?> MeAsync(CancellationToken ct = default)
  {
    try { return await GetAsync<MeResponse>("api/auth/me", ct); }
    catch { return null; }
  }
}
