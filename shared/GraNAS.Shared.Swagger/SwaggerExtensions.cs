using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace GraNAS.Shared.Swagger;

public static class SwaggerExtensions
{
  public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services, string apiTitle, string apiVersion)
  {
    services.AddSwaggerGen(c =>
    {
      // 1. Базовая информация об API
      c.SwaggerDoc(apiVersion, new OpenApiInfo { Title = apiTitle, Version = apiVersion });

      // 2. Путь к XML-файлу документации
      var xmlFile = $"{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}.xml";
      var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
      c.IncludeXmlComments(xmlPath);

      // 3. Определение схемы безопасности Bearer
      c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
      {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
      });

      // 4. Требование безопасности
      c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
      {
        { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() }
      });
    });

    return services;
  }

  public static IApplicationBuilder UseSwaggerWithJwt
  (this IApplicationBuilder builder, string apiTitle,
    string apiVersion)
  {
    builder.UseSwagger();
    builder.UseSwaggerUI(c =>
    {
      c.SwaggerEndpoint($"/swagger/{apiVersion}/swagger.json",
        $"{apiTitle} {apiVersion}");
      c.RoutePrefix = "swagger";
    });

    return builder;
  }
}
