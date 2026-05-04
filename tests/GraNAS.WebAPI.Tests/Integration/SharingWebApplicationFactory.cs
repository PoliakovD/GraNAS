using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GraNAS.Shared.Messaging.Abstractions;
using GraNAS.Sharing.API;
using GraNAS.Sharing.DAL;
using GraNAS.Sharing.Services.Interfaces;
using GraNAS.Shared.LoggingService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Testcontainers.PostgreSql;

namespace GraNAS.WebAPI.Tests.Integration;

public sealed class SharingWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("sharingtest")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Mock<IMetadataServiceClient> MetadataClientMock { get; } = new();
    public Mock<IEventPublisher> EventPublisherMock { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SharingDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            services.Add(ServiceDescriptor.Scoped<SharingDbContext>(_ =>
                new SharingDbContext(
                    new DbContextOptionsBuilder<SharingDbContext>()
                        .UseNpgsql(_postgres.GetConnectionString())
                        .Options)));

            var metaDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IMetadataServiceClient));
            if (metaDesc != null) services.Remove(metaDesc);
            services.AddSingleton<IMetadataServiceClient>(_ => MetadataClientMock.Object);

            var publisherDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IEventPublisher));
            if (publisherDesc != null) services.Remove(publisherDesc);
            services.AddSingleton<IEventPublisher>(_ => EventPublisherMock.Object);

            var loggerDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ILoggerService));
            if (loggerDesc != null) services.Remove(loggerDesc);
            services.AddSingleton<ILoggerService>(Mock.Of<ILoggerService>());
        });
    }

    internal string GenerateJwt(Guid userId)
    {
        var jwt = Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
