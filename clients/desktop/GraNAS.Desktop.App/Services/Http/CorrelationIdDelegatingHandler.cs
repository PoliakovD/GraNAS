using System.Net.Http.Headers;
using GraNAS.Desktop.App.Services.Auth;

namespace GraNAS.Desktop.App.Services.Httpp;

public class CorrelationIdDelegatingHandler : DelegatingHandler
{
  protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request, CancellationToken ct)
  {
    if (correlationId().AccessToken is { } token)
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    return base.SendAsync(request, ct);
  }
}
