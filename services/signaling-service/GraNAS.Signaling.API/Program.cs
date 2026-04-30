using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GraNAS.Signaling.API.Hubs;
using GraNAS.Signaling.API.Infrastructure;
using GraNAS.Signaling.DAL;
using GraNAS.Signaling.DAL.Extensions;
using GraNAS.Signaling.Services.Extensions;
using GraNAS.Signaling.Services.Interfaces;
using GraNAS.Shared.Correlation;
using GraNAS.Shared.Infrastructure.Middleware;
using GraNAS.Shared.LoggingService;
using GraNAS.Shared.Models.DTO;
using GraNAS.Shared.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using StackExchange.Redis;

namespace GraNAS.Signaling.API;

public class Program
{
  public static async Task Main(string[] args)
  {
    const string versionApi = "v1";
    const string apiTitle = "GraNAS.Signaling.API";
    const string corsPolicyName = "SignalingCors";

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) =>
    {
      var esUri = ctx.Configuration["Elasticsearch:Uri"]
                  ?? throw new InvalidOperationException("Elasticsearch:Uri is not configured");
      cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", apiTitle)
        .WriteTo.Console()
        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(esUri))
        {
          AutoRegisterTemplate = true,
          IndexFormat = "granas-logs-{0:yyyy.MM.dd}"
        });
    });

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
      options.InvalidModelStateResponseFactory = context =>
      {
        var errors = context.ModelState
          .Where(e => e.Value!.Errors.Count > 0)
          .SelectMany(e => e.Value!.Errors.Select(er => er.ErrorMessage))
          .ToList();
        return new BadRequestObjectResult(new ErrorResponse
        {
          Error = "validation_error",
          ErrorDescription = errors.FirstOrDefault() ?? "Validation failed."
        });
      };
    });

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddCors(options =>
    {
      options.AddPolicy(corsPolicyName, policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    });

    builder.Services.AddScoped<ILoggerService, LoggerService>();

    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

    builder.Services.AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
      })
      .AddJwtBearer(options =>
      {
        options.RequireHttpsMetadata = !builder.Environment.IsEnvironment("Test");
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidateAudience = true,
          ValidateLifetime = true,
          ValidateIssuerSigningKey = true,
          ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
          ValidIssuer = jwtSettings["Issuer"],
          ValidAudience = jwtSettings["Audience"],
          IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
        options.Events = new JwtBearerEvents
        {
          // SignalR передаёт JWT через query string для WebSocket
          OnMessageReceived = ctx =>
          {
            var accessToken = ctx.Request.Query["access_token"];
            var path = ctx.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs/signaling"))
              ctx.Token = accessToken;
            return Task.CompletedTask;
          },
          OnAuthenticationFailed = ctx =>
          {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ctx.Exception, "JWT authentication failed");
            return Task.CompletedTask;
          }
        };
      });

    var dbConnectionString = builder.Configuration["ConnectionStrings:SignalingDb"]
                             ?? throw new InvalidOperationException("ConnectionStrings:SignalingDb is not configured");
    builder.Services.AddDbContext<SignalingDbContext>(opts => opts.UseNpgsql(dbConnectionString));
    builder.Services.AddSignalingDal();

    var redisConnectionString = builder.Configuration["ConnectionStrings:Redis"]
                                ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured");

    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));

    // SignalR с Redis backplane для горизонтального масштабирования
    builder.Services.AddSignalR()
      .AddStackExchangeRedis(redisConnectionString, opts =>
        opts.Configuration.ChannelPrefix = RedisChannel.Literal("signaling"));

    builder.Services.AddHttpClient<IMetadataServiceClient, MetadataServiceClient>(c =>
    {
      var baseUrl = builder.Configuration["MetadataService:BaseUrl"]
                    ?? throw new InvalidOperationException("MetadataService:BaseUrl is not configured");
      c.BaseAddress = new Uri(baseUrl);
    });

    builder.Services.AddHttpClient<ISharingServiceClient, SharingServiceClient>(c =>
      {
        var baseUrl = builder.Configuration["SharingService:BaseUrl"]
                      ?? throw new InvalidOperationException("SharingService:BaseUrl is not configured");
        c.BaseAddress = new Uri(baseUrl);
      })
      .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

    builder.Services.AddControllers();

    builder.Services.AddHealthChecks()
      .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"]);

    builder.Services.AddHsts(options =>
    {
      options.Preload = true;
      options.IncludeSubDomains = true;
      options.MaxAge = TimeSpan.FromDays(365);
    });

    builder.Services.AddSignalingApplication();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerWithJwt(apiTitle, versionApi);

    builder.Services.AddCorrelationId();

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseCorrelationId();
    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
      app.UseHttpsRedirection();
      app.UseHsts();
    }

    app.UseSecurityHeaders(policy =>
    {
      policy.AddDefaultSecurityHeaders();
      policy.AddFrameOptionsDeny();
      policy.AddXssProtectionBlock();
      policy.AddContentTypeOptionsNoSniff();
      policy.AddStrictTransportSecurityMaxAge((int)TimeSpan.FromDays(365).TotalSeconds);
    });

    app.UseCors(corsPolicyName);
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
      app.UseSwaggerWithJwt(apiTitle, versionApi);
    }

    app.MapControllers();
    app.MapHub<SignalingHub>("/hubs/signaling");

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
      Predicate = c => c.Tags.Contains("live")
    }).AllowAnonymous();

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
      Predicate = _ => true
    }).AllowAnonymous();

    await app.RunAsync();
  }
}
