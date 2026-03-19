using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using GraNAS.Models;
using GraNAS.Models.DTO;
using GraNAS.WebAPI.DAL;
using GraNAS.WebAPI.DAL.Repositories.Implementation;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using GraNAS.WebAPI.Extensions;
using GraNAS.WebAPI.Middleware;
using GraNAS.WebAPI.Services.Implementations;
using GraNAS.WebAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;


namespace GraNAS.WebAPI;

public class Program
{
  public static async Task Main(string[] args)
  {
    const string versionApi = "v1";
    const string corsPolicyName = "MyAllowSpecificOrigins";

    var builder = WebApplication.CreateBuilder(args);


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

    // добавляем бд
    builder.AddPostgreSql();

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
          IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        };

        if (!builder.Environment.IsEnvironment("Test"))
        {
          options.Events = new JwtBearerEvents
          {
            OnAuthenticationFailed = context =>
            {
              var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
              logger.LogError(context.Exception, "JWT authentication failed for user: {User}, Token: {Token}",
                context.HttpContext.User?.Identity?.Name,
                context.Request.Headers.Authorization.ToString());
              return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
              var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
              logger.LogWarning(
                "JWT challenge issued: {Error}, Description: {ErrorDescription}, Path: {Path}, Scheme: {Scheme}, Header{Header}",
                context.Error,
                context.ErrorDescription,
                context.HttpContext.Request.Path,
                context.Scheme.Name,
                context.Request.Headers.Authorization.ToString());
              return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
              var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
              var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? context.Principal?.FindFirst("sub")?.Value;
              logger.LogInformation("JWT successfully validated for user ID: {UserId} at {ValidationTime}",
                userId, DateTime.UtcNow);
              return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
              var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
              if (string.IsNullOrEmpty(context.Token))
              {
                logger.LogDebug("No JWT token found in request for path: {Path}", context.HttpContext.Request.Path);
              }
              else
              {
                logger.LogDebug("JWT token received for user agent: {UserAgent}, Path: {Path}",
                  context.HttpContext.Request.Headers.UserAgent,
                  context.HttpContext.Request.Path);
              }

              return Task.CompletedTask;
            }
          };
        }
        else
        {
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
        }
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
    builder.Services.AddScoped<IFolderRepository, FolderRepository>();
    builder.Services.AddScoped<IFileRepository, FileRepository>();

    builder.Services.AddEndpointsApiExplorer();

    // Рабочая настройка

    // Настройка Swagger с поддержкой JWT
    builder.Services.AddSwaggerGen(c =>
    {
      c.SwaggerDoc("v1", new OpenApiInfo { Title = builder.Environment.ApplicationName, Version = "v1" });

      // Включение XML-комментариев
      var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
      var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
      c.IncludeXmlComments(xmlPath);

      // Добавляем возможность вводить токен в Swagger UI
      c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
      {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
      });

      // // Требование безопасности: создаём ссылку на схему "Bearer"
      c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
      {
        { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() }
      });
    });

    // Рабочая настройка


    WebApplication app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // HttpsRedirection – отключаем в тестах
    // HSTS – тоже отключаем в тестах
    if (!app.Environment.IsEnvironment("Testing"))
    {
      app.UseHttpsRedirection();
      app.UseHsts();
    }


    app.UseSecurityHeaders(policy =>
    {
      policy.AddDefaultSecurityHeaders(); // добавляет рекомендуемые заголовки (X-Content-Type-Options, X-Frame-Options и др.)
      // или кастомные:
      policy.AddFrameOptionsDeny();
      policy.AddXssProtectionBlock();
      policy.AddContentTypeOptionsNoSniff();
      policy.AddStrictTransportSecurityMaxAge(TimeSpan.FromDays(30).Seconds);
    });


    app.UseCors(corsPolicyName);

    app.UseRateLimiter();

    app.UseAuthentication(); // Добавляем аутентификацию
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint($"/swagger/{versionApi}/swagger.json",
          $"{builder.Environment.ApplicationName} {versionApi}");
        c.RoutePrefix = "swagger";
      });
    }

    ;

    app.MapControllers();

    await app.RunAsync();
  }
}
