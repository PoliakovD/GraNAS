using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GraNAS.Metadata.API;
using GraNAS.Metadata.DAL;
using GraNAS.Shared.LoggingService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Testcontainers.PostgreSql;

namespace GraNAS.WebAPI.Tests.Integration;

public sealed class MetadataWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    internal const string JwtSecret = "TestSecretKeyForAuthTests-AtLeast32Chars!";
    internal const string JwtIssuer = "GraNAS";
    internal const string JwtAudience = "GraNASClients";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("metadatatest")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
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
            services.Add(ServiceDescriptor.Scoped<MetadataDbContext>(_ =>
                new MetadataDbContext(
                    new DbContextOptionsBuilder<MetadataDbContext>()
                        .UseNpgsql(_postgres.GetConnectionString())
                        .Options)));

            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                opts =>
                {
                    opts.RequireHttpsMetadata = false;
                    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
                    opts.TokenValidationParameters.IssuerSigningKey = key;
                    opts.TokenValidationParameters.ValidIssuer = JwtIssuer;
                    opts.TokenValidationParameters.ValidAudience = JwtAudience;
                });

            var loggerDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ILoggerService));
            if (loggerDescriptor != null) services.Remove(loggerDescriptor);
            services.AddSingleton<ILoggerService>(Mock.Of<ILoggerService>());
        });
    }

    internal string GenerateJwt(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
