using Avalonia;
using Avalonia.ReactiveUI;
using GraNAS.Desktop.App;
using Microsoft.Extensions.Configuration;
using Serilog;

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

var config = new ConfigurationBuilder()
  .SetBasePath(AppContext.BaseDirectory)
  .AddJsonFile("appsettings.json", optional: false)
  .AddJsonFile($"appsettings.{environment}.json", optional: true)
  .AddEnvironmentVariables()
  .Build();

Log.Logger = new LoggerConfiguration()
  .ReadFrom.Configuration(config)
  .CreateLogger();

try
{
  AppBuilder.Configure(() => new App(config))
    .UsePlatformDetect()
    .WithInterFont()
    .LogToTrace()
    .UseReactiveUI()
    .StartWithClassicDesktopLifetime(args);
}
finally
{
  Log.CloseAndFlush();
}
