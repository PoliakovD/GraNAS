using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GraNAS.WebAPI.DAL;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
  public AppDbContext CreateDbContext(string[] args)
  {
    // Загружаем конфигурацию из appsettings.json
    var configuration = new ConfigurationBuilder()
      .SetBasePath(Directory.GetCurrentDirectory()) // Путь к проекту DAL
      .AddJsonFile("appsettings.json")
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
