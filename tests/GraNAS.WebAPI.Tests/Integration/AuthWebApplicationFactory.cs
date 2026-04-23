using System.Text;
using GraNAS.Auth.API;
using GraNAS.Auth.DAL;
using GraNAS.Shared.LoggingService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Testcontainers.PostgreSql;

namespace GraNAS.WebAPI.Tests.Integration;

/// <summary>
/// Фабрика поднимает PostgreSQL в Docker-контейнере, применяет EF-миграции
/// и переопределяет инфраструктурные зависимости (rate-limiter, JWT, ILoggerService).
/// </summary>
public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("authtest")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    // ─── IAsyncLifetime ───────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Первый доступ к Services собирает хост; после этого применяем миграции.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        Dispose(); // WebApplicationFactory.Dispose()
    }

    // ─── WebApplicationFactory ────────────────────────────────────────────────

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Test" загружает appsettings.Test.json с правильным JWT-ключом.
        // Program.cs отключает UseHttpsRedirection только в IsDevelopment(),
        // но тестовый in-memory сервер не поднимает реальный TLS —
        // редирект просто не срабатывает.
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // 1. AppDbContext — last-wins: наш дескриптор регистрируется последним
            //    и возвращается при GetService<AppDbContext>().
            //    GetConnectionString() вызывается в момент разрешения зависимости
            //    (уже после InitializeAsync → контейнер запущен).
            services.Add(ServiceDescriptor.Scoped<AppDbContext>(_ =>
                new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>()
                        .UseNpgsql(_postgres.GetConnectionString())
                        .Options)));

            // 2. JWT: явно фиксируем подписывающий ключ через PostConfigure,
            //    чтобы он гарантированно совпадал с тем, что использует JwtTokenService.
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                opts =>
                {
                    opts.RequireHttpsMetadata = false;
                    var key = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("TestSecretKeyForAuthTests-AtLeast32Chars!"));
                    opts.TokenValidationParameters.IssuerSigningKey = key;
                    opts.TokenValidationParameters.ValidIssuer    = "GraNAS";
                    opts.TokenValidationParameters.ValidAudience  = "GraNASClients";
                });

            // 3. Rate-limiter: удаляем ВСЕ IConfigureOptions<RateLimiterOptions>
            //    (включая политику "auth" из Program.cs) и заменяем безлимитной.
            var rateLimiterConfigs = services
                .Where(d => d.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>))
                .ToList();
            foreach (var d in rateLimiterConfigs) services.Remove(d);

            services.Configure<RateLimiterOptions>(opts =>
                opts.AddFixedWindowLimiter("auth", p =>
                {
                    p.PermitLimit          = int.MaxValue;
                    p.Window               = TimeSpan.FromMinutes(1);
                    p.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                    p.QueueLimit           = 0;
                }));

            // 4. ILoggerService: мок, чтобы не требовался RabbitMQ.
            var loggerDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ILoggerService));
            if (loggerDescriptor != null) services.Remove(loggerDescriptor);
            services.AddSingleton<ILoggerService>(Mock.Of<ILoggerService>());
        });
    }
}
