using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GraNAS.Auth.DAL;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
  public AppDbContext CreateDbContext(string[] args)
  {
    // Загружаем конфигурацию из appsettings.json
    var configuration = new ConfigurationBuilder()
      .AddJsonFile("appsettings.json", true)
      .AddEnvironmentVariables()
      .Build();

    var connectionString = configuration.GetConnectionString("Default");

    if (string.IsNullOrEmpty(connectionString))
    {
      throw new InvalidOperationException(
        "Не найдена строка подключения 'DefaultConnection' в appsettings.json");
    }

    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
    optionsBuilder.UseNpgsql(connectionString);

    return new AppDbContext(optionsBuilder.Options);
  }

}
