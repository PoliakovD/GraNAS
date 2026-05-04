using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GraNAS.Signaling.API;
using GraNAS.Signaling.DAL;
using GraNAS.Signaling.Services.Interfaces;
using GraNAS.Shared.LoggingService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace GraNAS.WebAPI.Tests.Integration;

public sealed class SignalingWebApplicationFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7.2-alpine")
        .Build();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("signalingdb_test")
        .WithUsername("signaling")
        .WithPassword("signaling_test")
        .Build();

    public Mock<IMetadataServiceClient> MetadataClientMock { get; } = new();
    public Mock<ISharingServiceClient> SharingClientMock { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_redis.StartAsync(), _postgres.StartAsync());
        await EnsureDatabaseCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Task.WhenAll(_redis.DisposeAsync().AsTask(), _postgres.DisposeAsync().AsTask());
        Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
                ["ConnectionStrings:SignalingDb"] = _postgres.GetConnectionString()
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace IConnectionMultiplexer with test Redis
            var muxDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (muxDesc != null) services.Remove(muxDesc);
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(_redis.GetConnectionString()));

            // Replace DbContext with test Postgres
            var dbDesc = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<SignalingDbContext>));
            if (dbDesc != null) services.Remove(dbDesc);
            services.AddDbContext<SignalingDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString()));

            // Replace inter-service HTTP clients with mocks
            var metaDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IMetadataServiceClient));
            if (metaDesc != null) services.Remove(metaDesc);
            services.AddSingleton<IMetadataServiceClient>(_ => MetadataClientMock.Object);

            var sharingDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ISharingServiceClient));
            if (sharingDesc != null) services.Remove(sharingDesc);
            services.AddSingleton<ISharingServiceClient>(_ => SharingClientMock.Object);

            // Suppress Serilog LoggerService (no RabbitMQ in tests)
            var loggerDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ILoggerService));
            if (loggerDesc != null) services.Remove(loggerDesc);
            services.AddSingleton<ILoggerService>(Mock.Of<ILoggerService>());
        });
    }

    // Apply EF migrations on first use
    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalingDbContext>();
        await db.Database.MigrateAsync();
    }

    internal string GenerateJwt(Guid userId, string? email = null)
    {
        var jwt = Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId.ToString()) };
        if (email != null) claims.Add(new(JwtRegisteredClaimNames.Email, email));
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
