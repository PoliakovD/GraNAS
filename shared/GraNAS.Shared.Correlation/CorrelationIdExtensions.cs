using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Shared.Correlation
{
  public static class CorrelationIdExtensions
  {
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
      services.AddHttpContextAccessor();
      services.AddTransient<CorrelationIdDelegatingHandler>();
      return services;
    }

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
      return app.UseMiddleware<CorrelationIdMiddleware>();
    }
  }
}
