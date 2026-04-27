using System.Net;
using Polly;
using Polly.Extensions.Http;

namespace GraNAS.Desktop.App.Services.Http;

/// <summary>
/// Retries on transient network errors (5xx, timeout, connection refused).
/// Does NOT retry 401 — that's RefreshOn401Handler's job.
/// </summary>
public static class RetryPolicy
{
  private static readonly TimeSpan[] Delays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)];

  public static IAsyncPolicy<HttpResponseMessage> Build()
    => HttpPolicyExtensions
      .HandleTransientHttpError()
      .OrResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
      .WaitAndRetryAsync(Delays);
}
