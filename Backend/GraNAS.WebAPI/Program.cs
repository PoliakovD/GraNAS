using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GraNAS.Models;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

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
          IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
      });

    // Добавление авторизации
    builder.Services.AddAuthorization();

    // Добавление контроллеров
    builder.Services.AddControllers();

    // HttpsRedirection
    builder.Services.AddHttpsRedirection(options =>
    {
      options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
      options.HttpsPort = 44344;
    });

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

    builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    builder.Services.AddEndpointsApiExplorer();
    // Настройка Swagger с поддержкой JWT
    builder.Services.AddSwaggerGen(c =>
    {
      c.SwaggerDoc("v1", new OpenApiInfo { Title = builder.Environment.ApplicationName, Version = "v1" });

      // Добавляем возможность вводить токен в Swagger UI
      c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
      {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
      });

      // Требование безопасности: создаём ссылку на схему "Bearer"
      c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
      {
        {
          new OpenApiSecuritySchemeReference("Bearer"), []
        }
      });
    });

    WebApplication app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();

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


    app.UseCors(corsPolicyName);

    // HttpsRedirection

    app.UseHttpsRedirection();

    //  HTTP Strict Transport Security Protocol (HSTS)

    app.UseHsts();

    app.UseAuthentication(); // Добавляем аутентификацию
    app.UseAuthorization();

    app.MapControllers();

    await app.RunAsync();
  }
}
