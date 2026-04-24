using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GraNAS.Sharing.DAL;

public class SharingDbContextFactory : IDesignTimeDbContextFactory<SharingDbContext>
{
    public SharingDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default");

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Не найдена строка подключения 'Default' в appsettings.json");

        var optionsBuilder = new DbContextOptionsBuilder<SharingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new SharingDbContext(optionsBuilder.Options);
    }
}
