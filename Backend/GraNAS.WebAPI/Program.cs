using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

namespace GraNAS.WebAPI;

public class Program
{
    public static async Task Main(string[] args)
    {

        const string versionApi = "v1";
        const string corsPolicyName = "MyAllowSpecificOrigins";

        var builder = WebApplication.CreateBuilder(args);
        var connectionString = builder.Configuration.GetConnectionString("Default");
        if (string.IsNullOrEmpty(connectionString))
            throw new Exception("Отсутствует строка подключения к БД");

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



        builder.Services.AddOpenApi();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc(versionApi,
                new OpenApiInfo { Title = builder.Environment.ApplicationName, Version = versionApi });
        });

        WebApplication app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint($"/swagger/{versionApi}/swagger.json",
                $"{builder.Environment.ApplicationName} {versionApi}"));
        }




        app.UseCors(corsPolicyName);

        // HttpsRedirection
        app.UseHttpsRedirection();

        //  HTTP Strict Transport Security Protocol (HSTS)
        app.UseHsts();

        //app.UseAuthorization();


        await app.RunAsync();
    }
}
