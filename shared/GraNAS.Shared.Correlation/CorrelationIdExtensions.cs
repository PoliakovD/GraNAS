using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Shared.Correlation
{
  public static class CorrelationIdExtensions
  {
    /// <summary>
    /// Добавляет сервисы для корреляции запросов:
    /// - IHttpContextAccessor
    /// - CorrelationIdDelegatingHandler (для исходящих HTTP-запросов)
    /// </summary>
    public static IServiceCollection (this IServiceCollection services)
    {
      services.AddHttpContextAccessor();
      services.AddTransient<CorrelationIdDelegatingHandler>();
      return services;
    }

    /// <summary>
    /// Добавляет middleware, которое:
    /// - Генерирует X-Correlation-ID, если его нет
    /// - Сохраняет его в TraceIdentifier и логи
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
      return app.UseMiddleware<CorrelationIdMiddleware>();
    }
  }
}
