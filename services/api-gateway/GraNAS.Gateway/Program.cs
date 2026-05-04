using System;
using GraNAS.Shared.Correlation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace GraNAS.Gateway;

public class Program
{
    public static void Main(string[] args)
    {
        const string corsPolicyName = "WebClient";

        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((ctx, cfg) =>
            cfg
                .ReadFrom.Configuration(ctx.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "GraNAS.Gateway")
                .WriteTo.Console());

        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        builder.Services.AddCors(o => o.AddPolicy(corsPolicyName, p =>
            p.WithOrigins(allowedOrigins)
             .AllowCredentials()
             .AllowAnyMethod()
             .AllowAnyHeader()));

        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        builder.Services.AddHealthChecks()
            .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"]);

        if (!builder.Environment.IsDevelopment())
        {
            builder.Services.AddHsts(o =>
            {
                o.Preload = true;
                o.IncludeSubDomains = true;
                o.MaxAge = TimeSpan.FromDays(365);
            });
        }

        builder.Services.AddCorrelationId();

        var app = builder.Build();

        app.UseCorrelationId();
        app.UseSerilogRequestLogging(opts =>
        {
            opts.GetLevel = (ctx, _, _) =>
                ctx.Request.Path.StartsWithSegments("/health")
                    ? LogEventLevel.Debug
                    : LogEventLevel.Information;
        });

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
            app.UseHsts();
        }

        app.UseCors(corsPolicyName);
        app.UseWebSockets();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("live")
        }).AllowAnonymous();

        app.MapReverseProxy();

        app.Run();
    }
}
