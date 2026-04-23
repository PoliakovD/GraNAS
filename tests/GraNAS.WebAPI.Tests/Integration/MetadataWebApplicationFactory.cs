using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GraNAS.Metadata.API;
using GraNAS.Metadata.DAL;
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

public sealed class MetadataWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
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

      var loggerDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILoggerService));
      if (loggerDescriptor != null) services.Remove(loggerDescriptor);
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
