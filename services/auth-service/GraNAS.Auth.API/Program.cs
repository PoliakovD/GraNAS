using System;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using GraNAS.Models.DTO;
using GraNAS.Shared.Correlation;
using GraNAS.Shared.Infrastructure.Extensions;
using GraNAS.Shared.Infrastructure.Middleware;
using GraNAS.Shared.LoggingService;
using GraNAS.Shared.Swagger;
using GraNAS.WebAPI.DAL.Repositories.Implementation;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using GraNAS.WebAPI.Services.Implementations;
using GraNAS.WebAPI.Services.Interfaces;
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

namespace GraNAS.WebAPI.Authorization;

public class Program
{
  public static async Task Main(string[] args)
  {
    const string versionApi = "v1";
    const string apiTitle = "GraNAS.Auth.API";
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
          .Where(e => e.Value.Errors.Count > 0)
          .SelectMany(e => e.Value.Errors.Select(er => er.ErrorMessage))
          .ToList();

        var errorResponse = new ErrorResponse
        {
          Error = "validation_error",
          ErrorDescription = errors.FirstOrDefault() ?? "One or more validation errors occurred."
        };

        return new BadRequestObjectResult(errorResponse);
      };
    });

    // Добавляем Корреляцию

    builder.Services.AddHttpContextAccessor();

    // добавляем бд
    builder.AddPostgreSql<GraNAS.WebAPI.DAL.AppDbContext>();

    // Настройка CORS
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

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ILoggerService, LoggerService>();

    // Настройка аутентификации JWT
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

    builder.Services.AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
      })
      .AddJwtBearer(options =>
      {
        options.RequireHttpsMetadata = true; // Требовать HTTPS для получения токена
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


    // HttpsRedirection
    builder.Services.AddHttpsRedirection(options =>
    {
      options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
      options.HttpsPort = 44344;
    });

    // Добавление контроллеров
    builder.Services.AddControllers();

    builder.Services.AddRateLimiter(options =>
    {
      // Добавляем именованную политику для аутентификационных эндпоинтов
      options.AddFixedWindowLimiter("auth", policy =>
      {
        policy.PermitLimit = 10; // максимум 10 запросов
        policy.Window = TimeSpan.FromMinutes(1); // в окне 1 минута
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0; // без очереди
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

    // Добавление авторизации
    builder.Services.AddAuthorization();


    //  HTTP Strict Transport Security Protocol (HSTS)
    builder.Services.AddHsts(options =>
    {
      options.Preload = true;
      options.IncludeSubDomains = true;
      options.MaxAge = TimeSpan.FromDays(365);
    });

    // Cобственная реализация jwt
    builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
    builder.Services.AddScoped<ITokenService, JwtTokenService>();

    // Репозитории
    builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    builder.Services.AddEndpointsApiExplorer();


    builder.Services.AddSwaggerWithJwt(apiTitle, versionApi);

    // Рабочая настройка

    WebApplication app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();

    // HttpsRedirection

    app.UseHttpsRedirection();
    //  HTTP Strict Transport Security Protocol (HSTS)

    app.UseHsts();
    app.UseSecurityHeaders(policy =>
    {
      policy.AddDefaultSecurityHeaders(); // добавляет рекомендуемые заголовки (X-Content-Type-Options, X-Frame-Options и др.)
      // или кастомные:
      policy.AddFrameOptionsDeny();
      policy.AddXssProtectionBlock();
      policy.AddContentTypeOptionsNoSniff();
      policy.AddStrictTransportSecurityMaxAge(TimeSpan.FromDays(300).Seconds);
    });


    app.UseCors(corsPolicyName);

    app.UseRateLimiter();

    app.UseAuthentication(); // Добавляем аутентификацию
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
    };

    app.MapControllers();

    await app.RunAsync();
  }
}
