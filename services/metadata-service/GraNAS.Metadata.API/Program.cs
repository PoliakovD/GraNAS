using System;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using GraNAS.Metadata.API.Authorization;
using GraNAS.Metadata.API.Infrastructure;
using GraNAS.Metadata.DAL;
using GraNAS.Metadata.DAL.Extensions;
using GraNAS.Metadata.Models;
using GraNAS.Metadata.Services.Extensions;
using GraNAS.Metadata.Services.Interfaces;
using GraNAS.Shared.Correlation;
using GraNAS.Shared.Infrastructure.Extensions;
using GraNAS.Shared.Infrastructure.Middleware;
using GraNAS.Shared.LoggingService;
using GraNAS.Shared.Models.DTO;
using GraNAS.Shared.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace GraNAS.Metadata.API;

public class Program
{
  public static async Task Main(string[] args)
  {
    const string versionApi = "v1";
    const string apiTitle = "GraNAS.Metadata.API";
    const string corsPolicyName = "MyAllowSpecificOrigins";

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

        var errorResponse = new ErrorResponse
        {
          Error = "validation_error",
          ErrorDescription = errors.FirstOrDefault() ?? "One or more validation errors occurred."
        };

        return new BadRequestObjectResult(errorResponse);
      };
    });

    builder.Services.AddHttpContextAccessor();

    builder.AddPostgreSql<MetadataDbContext>();

    builder.Services.AddCors(options =>
    {
      options.AddPolicy(corsPolicyName,
        policy =>
        {
          policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
        });
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
          OnAuthenticationFailed = context =>
          {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, "JWT authentication failed");
            return Task.CompletedTask;
          }
        };
      });

    builder.Services.AddHttpsRedirection(options =>
    {
      options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
      options.HttpsPort = 44344;
    });

    builder.Services.AddHttpClient<IAuthServiceClient, AuthServiceClient>(c =>
    {
      var baseUrl = builder.Configuration["AuthService:BaseUrl"]
                    ?? throw new InvalidOperationException("AuthService:BaseUrl is not configured");
      c.BaseAddress = new Uri(baseUrl);
    });

    builder.Services.AddControllers()
      .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddAuthorization(options =>
    {
      options.AddPolicy("CanReadFolder",
        p => p.Requirements.Add(new FolderAccessRequirement(AccessLevel.View)));
      options.AddPolicy("CanWriteFolder",
        p => p.Requirements.Add(new FolderAccessRequirement(AccessLevel.Full)));
    });
    builder.Services.AddScoped<IAuthorizationHandler, FolderAccessHandler>();

    builder.Services.AddRateLimiter(options =>
    {
      options.AddFixedWindowLimiter("api", policy =>
      {
        policy.PermitLimit = 60;
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
      .AddDbContextCheck<MetadataDbContext>("database", tags: ["ready"]);

    builder.Services.AddHsts(options =>
    {
      options.Preload = true;
      options.IncludeSubDomains = true;
      options.MaxAge = TimeSpan.FromDays(365);
    });

    // Composition root: регистрация слоёв
    builder.Services.AddMetadataDal();
    builder.Services.AddMetadataApplication();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerWithJwt(apiTitle, versionApi);

    WebApplication app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();
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
      policy.AddStrictTransportSecurityMaxAge(TimeSpan.FromDays(300).Seconds);
    });

    app.UseCors(corsPolicyName);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
      app.UseSwaggerWithJwt(apiTitle, versionApi);
      app.UseSwagger();
      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint($"/swagger/{versionApi}/swagger.json",
          $"{builder.Environment.ApplicationName} {versionApi}");
        c.RoutePrefix = "swagger";
      });
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

    await app.RunAsync();
  }
}
