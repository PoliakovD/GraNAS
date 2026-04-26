using System.Net;
using System.Net.Http.Headers;
using GraNAS.Desktop.App.Services.Auth;

namespace GraNAS.Desktop.App.Services.Http;

public class RefreshOn401Handler : DelegatingHandler
{
  private const string RetryKey = "GraNAS.Retry";
  private readonly Func<IAuthSession> _sessionFactory;

  public RefreshOn401Handler(Func<IAuthSession> sessionFactory)
  {
    _sessionFactory = sessionFactory;
  }

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request, CancellationToken ct)
  {
    var response = await base.SendAsync(request, ct);

    if (response.StatusCode != HttpStatusCode.Unauthorized
        || request.Options.TryGetValue(new HttpRequestOptionsKey<bool>(RetryKey), out var alreadyRetried) && alreadyRetried)
    {
      return response;
    }

    var session = _sessionFactory();
    var refreshed = await session.RefreshAsync(ct);
    if (!refreshed)
      return response;

    var retry = await CloneRequestAsync(request);
    retry.Options.Set(new HttpRequestOptionsKey<bool>(RetryKey), true);

    if (session.AccessToken is { } newToken)
      retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

    return await base.SendAsync(retry, ct);
  }

  private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
  {
    var clone = new HttpRequestMessage(original.Method, original.RequestUri);
    foreach (var header in original.Headers)
      clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

    if (original.Content is { } content)
    {
      var bytes = await content.ReadAsByteArrayAsync();
      clone.Content = new ByteArrayContent(bytes);
      foreach (var h in original.Content.Headers)
        clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
    }

    return clone;
  }
}
