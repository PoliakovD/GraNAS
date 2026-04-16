using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace GraNAS.Shared.Correlation;

public class CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var correlationId = httpContextAccessor.HttpContext?.TraceIdentifier;
    if (!string.IsNullOrEmpty(correlationId))
    {
      request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
    }

    return await base.SendAsync(request, cancellationToken);
  }
}
