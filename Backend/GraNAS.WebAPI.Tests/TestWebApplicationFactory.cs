using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using GraNAS.WebAPI.DAL;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;

namespace GraNAS.WebAPI.Tests;

public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Загружаем основную конфигурацию
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            // Переопределяем строку подключения для тестовой БД
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Host=localhost;Port=5432;Database=granas_test;Username=admin;Password=1234" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Удаляем предыдущую регистрацию DbContext
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Добавляем тестовую БД
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql("Host=localhost;Port=5432;Database=granas_test;Username=admin;Password=1234"));

            // Отключаем требование HTTPS для JWT в тестах
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
            });
        });

        // Устанавливаем окружение "Testing" для условного отключения HTTPS middleware
        builder.UseEnvironment("Testing");

        // Запускаем сервер на HTTP (не HTTPS)
        builder.UseUrls("http://localhost:5000");
    }

    public AppDbContext CreateContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
