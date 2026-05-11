using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using GraNAS.Desktop.App.Models;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.Http;
using GraNAS.Desktop.App.Services.P2P;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.App.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;

namespace GraNAS.Desktop.App;

/// <summary>
/// Корневой класс Avalonia-приложения GraNAS Desktop.
/// Отвечает за инициализацию DI-контейнера и запуск главного окна.
/// </summary>
public partial class App : Application
{
  private readonly IConfiguration _config;
  private IServiceProvider? _services;
  /// <summary>DI-контейнер приложения; доступен после <see cref="OnFrameworkInitializationCompleted"/>.</summary>
  public IServiceProvider? Services => _services;

  public App(IConfiguration config)
  {
    _config = config;
  }

  /// <summary>Загружает XAML-разметку приложения.</summary>
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  /// <summary>
  /// Строит DI-контейнер, создаёт главное окно и запускает восстановление сессии.
  /// При успешном восстановлении JWT автоматически подключает P2P, если <c>ShouldBeOnline=true</c>.
  /// </summary>
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
        try
        {
          await session.TryRestoreAsync();
          // Auto-connect P2P on restore (ShouldBeOnline defaults to true)
          if (session.IsAuthenticated)
          {
            var p2pHost = _services.GetRequiredService<IP2PHost>();
            if (p2pHost.ShouldBeOnline)
            {
              try { await p2pHost.ConnectAsync(); }
              catch { /* non-fatal: P2P is optional at startup */ }
            }
          }
        }
        catch { /* IsAuthenticated stays false → ShellViewModel already showing Login */ }
      };
    }

    base.OnFrameworkInitializationCompleted();
  }

  /// <summary>
  /// Конфигурирует DI-сервисы приложения.
  /// </summary>
  /// <remarks>
  /// Особенности:
  /// <list type="bullet">
  /// <item><c>IAuthSession</c> регистрируется последним, чтобы разорвать цикл: <c>AuthSession → AuthApi → BearerHandler → AuthSession</c>.</item>
  /// <item>P2P-сервисы (<see cref="IP2PHost"/>, <see cref="IFolderShareRegistry"/>, <see cref="IDeviceIdentity"/>) — Singleton.</item>
  /// <item>Hub URL формируется как <c>ApiGateway:BaseUrl + Signaling:HubPath</c>.</item>
  /// <item>Все HttpClient'ы оснащены <c>BearerTokenHandler</c> + <c>RefreshOn401Handler</c> + Polly-retry.</item>
  /// </list>
  /// </remarks>
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

    // P2P services
    services.AddSingleton<IFolderShareRegistry, FolderShareRegistry>();
    services.AddSingleton<IDeviceIdentity, DeviceIdentity>();

    services.AddHttpClient<ISignalingApi, SignalingApi>(c => c.BaseAddress = baseUri)
      .AddHttpMessageHandler<BearerTokenHandler>()
      .AddHttpMessageHandler<RefreshOn401Handler>()
      .AddPolicyHandler(retry);

    var signalingHubPath = _config["Signaling:HubPath"] ?? "/hubs/signaling";
    var hubUrl = apiSettings.BaseUrl.TrimEnd('/') + signalingHubPath;
    services.AddSingleton<IP2PHost>(sp => new P2PHost(
      sp.GetRequiredService<IFolderShareRegistry>(),
      sp.GetRequiredService<IAuthSession>(),
      sp.GetRequiredService<ISignalingApi>(),
      sp.GetRequiredService<IDeviceIdentity>(),
      sp.GetRequiredService<INotificationService>(),
      hubUrl));

    services.AddSingleton<ShellViewModel>();

    return services.BuildServiceProvider();
  }
}
