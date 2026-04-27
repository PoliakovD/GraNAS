using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using GraNAS.Desktop.App.Models;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.Http;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.App.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;

namespace GraNAS.Desktop.App;

public partial class App : Application
{
  private readonly IConfiguration _config;
  private IServiceProvider? _services;
  public IServiceProvider? Services => _services;

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

      // Navigation is fully reactive via ShellViewModel.WhenAnyValue(IsAuthenticated).
      // window.Opened only triggers the restore attempt; navigation follows automatically.

      var window = new ShellWindow { DataContext = shell };
      desktop.MainWindow = window;

      window.Opened += async (_, _) =>
      {
        try { await session.TryRestoreAsync(); }
        catch { /* IsAuthenticated stays false → ShellViewModel already showing Login */ }
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
    services.AddSingleton<NotificationService>();
    services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
    services.AddSingleton<IDialogService, DialogService>();

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
    services.AddTransient(sp => new BearerTokenHandler(() => sp.GetRequiredService<IAuthSession>()));
    services.AddTransient(sp => new RefreshOn401Handler(() => sp.GetRequiredService<IAuthSession>()));

    var retry = RetryPolicy.Build();

    // AuthApi: no refresh handler (login/refresh/me don't need 401-retry)
    services.AddHttpClient<IAuthApi, AuthApi>(c => c.BaseAddress = baseUri)
      .AddHttpMessageHandler<BearerTokenHandler>()
      .AddPolicyHandler(retry);

    services.AddHttpClient<IFoldersApi, FoldersApi>(c => c.BaseAddress = baseUri)
      .AddHttpMessageHandler<BearerTokenHandler>()
      .AddHttpMessageHandler<RefreshOn401Handler>()
      .AddPolicyHandler(retry);

    services.AddHttpClient<IPermissionsApi, PermissionsApi>(c => c.BaseAddress = baseUri)
      .AddHttpMessageHandler<BearerTokenHandler>()
      .AddHttpMessageHandler<RefreshOn401Handler>()
      .AddPolicyHandler(retry);

    services.AddHttpClient<ISharesApi, SharesApi>(c => c.BaseAddress = baseUri)
      .AddHttpMessageHandler<BearerTokenHandler>()
      .AddHttpMessageHandler<RefreshOn401Handler>()
      .AddPolicyHandler(retry);

    services.AddSingleton<ShellViewModel>();

    return services.BuildServiceProvider();
  }
}
