using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GraNAS.Metadata.DAL;

public class MetadataDbContextFactory : IDesignTimeDbContextFactory<MetadataDbContext>
{
  public MetadataDbContext CreateDbContext(string[] args)
  {
    var configuration = new ConfigurationBuilder()
      .SetBasePath(Directory.GetCurrentDirectory())
      .AddJsonFile("appsettings.json", true)
      .AddEnvironmentVariables()
      .Build();

    var connectionString = configuration.GetConnectionString("Default");

    if (string.IsNullOrEmpty(connectionString))
      throw new InvalidOperationException("Не найдена строка подключения 'Default' в appsettings.json");

    var optionsBuilder = new DbContextOptionsBuilder<MetadataDbContext>();
    optionsBuilder.UseNpgsql(connectionString);

    return new MetadataDbContext(optionsBuilder.Options);
  }
}
