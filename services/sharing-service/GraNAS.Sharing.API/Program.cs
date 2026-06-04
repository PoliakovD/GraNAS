using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using GraNAS.Sharing.API.HostedServices;
using GraNAS.Sharing.API.Infrastructure;
using GraNAS.Shared.Messaging.DependencyInjection;
using GraNAS.Sharing.DAL;
using GraNAS.Sharing.DAL.Extensions;
using GraNAS.Sharing.Services.Extensions;
using GraNAS.Sharing.Services.Implementations;
using GraNAS.Sharing.Services.Interfaces;
using GraNAS.Shared.Correlation;
using Microsoft.Extensions.Configuration;
using GraNAS.Shared.Infrastructure.Extensions;
using GraNAS.Shared.Infrastructure.Middleware;
using GraNAS.Shared.LoggingService;
using GraNAS.Shared.Models.DTO;
using GraNAS.Shared.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

namespace GraNAS.Sharing.API;

public class Program
{
  public static async Task Main(string[] args)
  {
    const string versionApi = "v1";
    const string apiTitle = "GraNAS.Sharing.API";
    const string corsPolicyName = "MyAllowSpecificOrigins";

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseGraNasCentralLogging(apiTitle);

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
          ErrorDescription = errors.FirstOrDefault() ?? "One or more validation errors occurred."
        });
      };
    });

    builder.Services.AddHttpContextAccessor();
    builder.AddPostgreSql<SharingDbContext>();

    if (builder.Environment.IsDevelopment())
    {
      builder.Services.AddCors(options =>
      {
        options.AddPolicy(corsPolicyName, policy =>
          policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
      });
    }

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
          OnAuthenticationFailed = context =>
          {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            if (context.Exception is SecurityTokenExpiredException)
            {
              logger.LogWarning("JWT authentication failed: Token has expired.");
            }
            else
            {
              logger.LogError(context.Exception, "JWT authentication failed due to an unexpected error.");
            }
            return Task.CompletedTask;
          }
        };
      });

    builder.Services.AddHttpsRedirection(options =>
    {
      options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
      options.HttpsPort = 44344;
    });

    builder.Services.AddHttpClient<IMetadataServiceClient, MetadataServiceClient>(c =>
      {
        var baseUrl = builder.Configuration["MetadataService:BaseUrl"]
                      ?? throw new InvalidOperationException("MetadataService:BaseUrl is not configured");
        c.BaseAddress = new Uri(baseUrl);
      })
      .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();


    builder.Services.AddGraNasCentralLoggingMvc();
    builder.Services.AddControllers();

    builder.Services.AddRateLimiter(options =>
    {
      options.AddFixedWindowLimiter("api", policy =>
      {
        policy.PermitLimit = 60;
        policy.Window = TimeSpan.FromMinutes(1);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0;
      });

      options.AddFixedWindowLimiter("public", policy =>
      {
        policy.PermitLimit = 30;
        policy.Window = TimeSpan.FromMinutes(1);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0;
      });

      options.OnRejected = async (context, token) =>
      {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponse
        {
          Error = "too_many_requests",
          ErrorDescription = "Too many requests. Please try again later."
        });
      };
    });

    builder.Services.AddHealthChecks()
      .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
      .AddDbContextCheck<SharingDbContext>("database", tags: ["ready"]);

    builder.Services.AddHsts(options =>
    {
      options.Preload = true;
      options.IncludeSubDomains = true;
      options.MaxAge = TimeSpan.FromDays(365);
    });

    builder.Services.AddGraNasMessaging(builder.Configuration);
    builder.Services.AddHostedService<ExpiredShareCleanupService>();

    builder.Services.AddSharingDal();

    // ITokenEncryptionService: resolved lazily so WebApplicationFactory can override config via ConfigureWebHost
    builder.Services.AddSingleton<ITokenEncryptionService>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var keyB64 = cfg["ShareLinkEncryption:Key"]
            ?? throw new InvalidOperationException("ShareLinkEncryption:Key is not configured. " +
                "Generate: [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))");
        return new TokenEncryptionService(Convert.FromBase64String(keyB64));
    });

    builder.Services.AddSharingApplication();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerWithJwt(apiTitle, versionApi);

    builder.Services.AddCorrelationId();

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
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

    app.UseSecurityHeaders(policy =>
    {
      policy.AddDefaultSecurityHeaders();
      policy.AddFrameOptionsDeny();
      policy.AddXssProtectionBlock();
      policy.AddContentTypeOptionsNoSniff();
      policy.AddStrictTransportSecurityMaxAge((int)TimeSpan.FromDays(365).TotalSeconds);
    });

    if (app.Environment.IsDevelopment())
    {
      app.UseCors(corsPolicyName);
    }

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
      app.UseSwaggerWithJwt(apiTitle, versionApi);
    }

    app.MapControllers();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
      Predicate = c => c.Tags.Contains("live")
    }).AllowAnonymous().DisableRateLimiting();

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
      Predicate = c => c.Tags.Contains("ready")
    }).AllowAnonymous().DisableRateLimiting();

    try
    {
      using (var scope = app.Services.CreateScope())
      {
        var db = scope.ServiceProvider.GetRequiredService<SharingDbContext>();
        await db.Database.MigrateAsync();
      }

      await app.RunAsync();
    }
    finally
    {
      Log.CloseAndFlush();
    }
  }
}
