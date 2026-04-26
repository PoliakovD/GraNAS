using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GraNAS.Desktop.App.Models;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.Http;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.App.Views;
using GraNAS.Shared.Correlation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Desktop.App;

public partial class App : Application
{
  private readonly IConfiguration _config;
  private IServiceProvider? _services;

  public App(IConfiguration config)
  {
    _config = config;
  }

  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    _services = BuildServices();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      var shell = _services.GetRequiredService<ShellViewModel>();
      var session = _services.GetRequiredService<IAuthSession>();

      session.SessionExpired += (_, _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(shell.ShowLogin);

      var window = new ShellWindow { DataContext = shell };
      desktop.MainWindow = window;

      window.Opened += async (_, _) =>
      {
        var restored = await session.TryRestoreAsync();
        if (!restored)
          shell.ShowLogin();
      };
    }

    base.OnFrameworkInitializationCompleted();
  }

  private IServiceProvider BuildServices()
  {
    var services = new ServiceCollection();

    var apiSettings = _config.GetSection("ApiGateway").Get<ApiGatewaySettings>()
                      ?? new ApiGatewaySettings();
    var baseUri = new Uri(apiSettings.BaseUrl);

    services.AddSingleton<ICredentialStore, WindowsCredentialStore>();

    // AuthSession registered as singleton; delegates are wired after SP is built
    // to break the circular dep: AuthSession → AuthApi → BearerHandler → AuthSession
    services.AddSingleton<IAuthSession>(sp =>
    {
      var store = sp.GetRequiredService<ICredentialStore>();
      var authApi = sp.GetRequiredService<IAuthApi>();
      return new AuthSession(
        store,
        rt => authApi.RefreshAsync(rt),
        () => authApi.MeAsync());
    });

    // DelegatingHandlers use Func<IAuthSession> to break circular dep
    services.AddTransient<CorrelationIdDelegatingHandler>();
    services.AddTransient(sp => new BearerTokenHandler(() => sp.GetRequiredService<IAuthSession>()));
    services.AddTransient(sp => new RefreshOn401Handler(() => sp.GetRequiredService<IAuthSession>()));

    // AuthApi: no refresh handler (login/refresh/me don't need 401-retry)
    services.AddHttpClient<IAuthApi, AuthApi>(c => c.BaseAddress = baseUri)
      .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
      .AddHttpMessageHandler<BearerTokenHandler>();

    services.AddHttpClient<IFoldersApi, FoldersApi>(c => c.BaseAddress = baseUri)
      .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
      .AddHttpMessageHandler<BearerTokenHandler>()
      .AddHttpMessageHandler<RefreshOn401Handler>();

    services.AddHttpClient<IPermissionsApi, PermissionsApi>(c => c.BaseAddress = baseUri)
      .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
      .AddHttpMessageHandler<BearerTokenHandler>()
      .AddHttpMessageHandler<RefreshOn401Handler>();

    services.AddHttpClient<ISharesApi, SharesApi>(c => c.BaseAddress = baseUri)
      .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
      .AddHttpMessageHandler<BearerTokenHandler>()
      .AddHttpMessageHandler<RefreshOn401Handler>();

    services.AddSingleton<ShellViewModel>();

    return services.BuildServiceProvider();
  }
}
