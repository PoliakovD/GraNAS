using System;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using GraNAS.Auth.DAL;
using GraNAS.Auth.DAL.Extensions;
using GraNAS.Auth.Services.Extensions;
using GraNAS.Shared.Correlation;
using GraNAS.Shared.Infrastructure.Extensions;
using GraNAS.Shared.Infrastructure.Middleware;
using GraNAS.Shared.LoggingService;
using GraNAS.Shared.Models.DTO;
using GraNAS.Shared.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace GraNAS.Auth.API;

public class Program
{
  public static async Task Main(string[] args)
  {
    const string versionApi = "v1";
    const string apiTitle = "GraNAS.Auth.API";
    const string corsPolicyName = "MyAllowSpecificOrigins";

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddCorrelationId();

    // В тестовом окружении используем только консольный вывод
    builder.Host.UseSerilog((ctx, cfg) =>
    {
        cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", apiTitle);

        if (ctx.HostingEnvironment.IsEnvironment("Test"))
        {
            cfg.WriteTo.Console();
        }
        else
        {
            var esUri = ctx.Configuration["Elasticsearch:Uri"]
                        ?? throw new InvalidOperationException("Elasticsearch:Uri is not configured");
            cfg.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(esUri))
            {
                AutoRegisterTemplate = true,
                IndexFormat = "granas-logs-{0:yyyy.MM.dd}"
            });
        }
    });

    builder.Services.AddHttpContextAccessor();

    builder.AddPostgreSql<AppDbContext>();

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

    // JWT Configuration
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsEnvironment("Test"); // Отключаем для тестов
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
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT challenge: {Error}, {ErrorDescription}", context.Error, context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddHttpsRedirection(options =>
    {
      options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
      options.HttpsPort = 44344;
    });

    builder.Services.AddControllers();

    // Configure AFTER AddControllers so our factory is not overridden by ApiBehaviorOptionsSetup.
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

    builder.Services.AddRateLimiter(options =>
    {
      options.AddFixedWindowLimiter("auth", policy =>
      {
        policy.PermitLimit = 10;
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

    builder.Services.AddAuthorization();

    builder.Services.AddHsts(options =>
    {
      options.Preload = true;
      options.IncludeSubDomains = true;
      options.MaxAge = TimeSpan.FromDays(365);
    });

    // Composition root: регистрация слоёв
    builder.Services.AddAuthDal();
    builder.Services.AddAuthApplication();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerWithJwt(apiTitle, versionApi);

    WebApplication app = builder.Build();

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

    await app.RunAsync();
  }
}
