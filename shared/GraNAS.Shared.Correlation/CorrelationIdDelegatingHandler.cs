using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace GraNAS.Shared.Correlation;

/// <summary>
/// DelegatingHandler, который автоматически добавляет заголовок X-Correlation-ID
/// в исходящие HTTP-запросы, чтобы отслеживать запрос между микросервисами.
/// </summary>
public class CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
  protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Получаем CorrelationId из текущего контекста (установленного middleware)
        var correlationId = httpContextAccessor.HttpContext?.TraceIdentifier;
        if (!string.IsNullOrEmpty(correlationId))
        {
            // Добавляем заголовок во ВСЕ исходящие запросы
            request.Headers.Add("X-Correlation-ID", correlationId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
