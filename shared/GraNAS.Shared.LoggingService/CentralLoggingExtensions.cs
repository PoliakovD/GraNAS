using GraNAS.Shared.LoggingService.Enrichment;
using GraNAS.Shared.LoggingService.Mvc;
using GraNAS.Shared.LoggingService.RabbitMq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace GraNAS.Shared.LoggingService;

public static class CentralLoggingExtensions
{
    public static IHostBuilder UseGraNasCentralLogging(this IHostBuilder host, string serviceName) =>
        host.UseSerilog((ctx, _, cfg) =>
        {
            var env = ctx.HostingEnvironment.EnvironmentName;

            cfg.ReadFrom.Configuration(ctx.Configuration)
               .Enrich.FromLogContext()
               .Enrich.With(new SensitiveDataEnricher(isProduction: ctx.HostingEnvironment.IsProduction()))
               .Destructure.ToMaximumDepth(3)
               .Destructure.ToMaximumStringLength(1024)
               .Destructure.ToMaximumCollectionCount(32)
               .WriteTo.Console();

            if (!ctx.HostingEnvironment.IsEnvironment("Test"))
            {
                cfg.WriteTo.RabbitMq(new RabbitMqSinkOptions
                {
                    Host = ctx.Configuration["RabbitMQ:Host"] ?? "localhost",
                    Username = ctx.Configuration["RabbitMQ:Username"] ?? "guest",
                    Password = ctx.Configuration["RabbitMQ:Password"] ?? "guest",
                    Service = serviceName,
                    Environment = env
                });
            }
        });

    public static IServiceCollection AddGraNasCentralLoggingMvc(this IServiceCollection services) =>
        services.Configure<MvcOptions>(o => o.Filters.Add<MvcLoggingActionFilter>());
}
