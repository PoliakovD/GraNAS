using Avalonia;
using Avalonia.ReactiveUI;
using GraNAS.Desktop.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

// Route SIPSorcery internal logs (STUN/TURN/ICE) through Serilog for diagnostics
SIPSorcery.LogFactory.Set(LoggerFactory.Create(b =>
    b.AddSerilog(Log.Logger).SetMinimumLevel(LogLevel.Debug)));

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
