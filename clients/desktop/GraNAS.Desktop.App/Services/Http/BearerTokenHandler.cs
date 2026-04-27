using System.Net.Http.Headers;
using GraNAS.Desktop.App.Services.Auth;

namespace GraNAS.Desktop.App.Services.Http;

/// <summary>
/// Adds Authorization header from IAuthSession.
/// Uses a factory to avoid circular dependency: AuthSession→AuthApi→BearerTokenHandler→AuthSession.
/// </summary>
public class BearerTokenHandler : DelegatingHandler
{
  private readonly Func<IAuthSession> _sessionFactory;

  public BearerTokenHandler(Func<IAuthSession> sessionFactory)
  {
    _sessionFactory = sessionFactory;
  }

  protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request, CancellationToken ct)
  {
    if (_sessionFactory().AccessToken is { } token)
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    return base.SendAsync(request, ct);
  }
}
