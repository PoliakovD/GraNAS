using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GraNAS.Signaling.DAL;

/// <summary>
/// Фабрика <see cref="SignalingDbContext"/> для design-time инструментов EF Core (<c>dotnet-ef migrations</c>).
/// Читает строку подключения из <c>appsettings.json</c> или переменных окружения.
/// </summary>
public class SignalingDbContextFactory : IDesignTimeDbContextFactory<SignalingDbContext>
{
    /// <summary>Создаёт <see cref="SignalingDbContext"/> для выполнения миграций EF Core CLI.</summary>
    /// <exception cref="InvalidOperationException">Если <c>ConnectionStrings:SignalingDb</c> не задана.</exception>
    public SignalingDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["ConnectionStrings:SignalingDb"];

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("ConnectionStrings:SignalingDb is not configured");

        var optionsBuilder = new DbContextOptionsBuilder<SignalingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new SignalingDbContext(optionsBuilder.Options);
    }
}
