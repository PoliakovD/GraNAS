using System.Reactive;
using System.Reactive.Linq;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.P2P;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

/// <summary>
/// Главная ViewModel приложения. Управляет навигацией между страницами, состоянием аутентификации
/// и P2P-подключением.
/// </summary>
public class ShellViewModel : ViewModelBase
{
  private readonly IAuthSession _session;
  private readonly IAuthApi _authApi;
  private readonly IFoldersApi _foldersApi;
  private readonly IPermissionsApi _permissionsApi;
  private readonly ISharesApi _sharesApi;
  private readonly IDialogService _dialogs;
  private readonly INotificationService _notifications;
  private readonly IP2PHost _p2pHost;
  private readonly IFolderShareRegistry _registry;
  private readonly ISignalingApi _signalingApi;
  private readonly IDeviceIdentity _deviceIdentity;
  private readonly IClipboardService _clipboard;

  private ViewModelBase? _currentPage;
  private string _currentNav = "home";
  private bool _isOnline;

  public ViewModelBase? CurrentPage
  {
    get => _currentPage;
    private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
  }

  public string CurrentNav
  {
    get => _currentNav;
    set => this.RaiseAndSetIfChanged(ref _currentNav, value);
  }

  public bool IsOnline
  {
    get => _isOnline;
    private set => this.RaiseAndSetIfChanged(ref _isOnline, value);
  }

  public IAuthSession Session => _session;

  public ReactiveCommand<Unit, Unit> LogoutCommand { get; }
  public ReactiveCommand<Unit, Unit> NavHomeCommand { get; }
  public ReactiveCommand<Unit, Unit> NavFoldersCommand { get; }
  public ReactiveCommand<Unit, Unit> NavSharedCommand { get; }
  public ReactiveCommand<Unit, Unit> NavPublicShareCommand { get; }
  public ReactiveCommand<Unit, Unit> NavLinksCommand { get; }
  public ReactiveCommand<Unit, Unit> NavRecentCommand { get; }
  public ReactiveCommand<Unit, Unit> NavSettingsCommand { get; }
  public ReactiveCommand<Unit, Unit> OnlineToggleCommand { get; }

  public ShellViewModel(
    IAuthSession session,
    IAuthApi authApi,
    IFoldersApi foldersApi,
    IPermissionsApi permissionsApi,
    ISharesApi sharesApi,
    IDialogService dialogs,
    INotificationService notifications,
    IP2PHost p2pHost,
    IFolderShareRegistry registry,
    ISignalingApi signalingApi,
    IDeviceIdentity deviceIdentity,
    IClipboardService clipboard)
  {
    _session = session;
    _authApi = authApi;
    _foldersApi = foldersApi;
    _permissionsApi = permissionsApi;
    _sharesApi = sharesApi;
    _dialogs = dialogs;
    _notifications = notifications;
    _p2pHost = p2pHost;
    _registry = registry;
    _signalingApi = signalingApi;
    _deviceIdentity = deviceIdentity;
    _clipboard = clipboard;

    LogoutCommand = ReactiveCommand.CreateFromTask(LogoutAsync);
    NavHomeCommand = ReactiveCommand.Create(() => ShowHome());
    NavFoldersCommand = ReactiveCommand.Create(() => ShowFolders());
    NavSharedCommand = ReactiveCommand.Create(() => ShowSharedWithMe());
    NavPublicShareCommand = ReactiveCommand.Create(() => ShowPublicShare());
    NavLinksCommand = ReactiveCommand.Create(() => ShowLinks());
    NavRecentCommand = ReactiveCommand.Create(() => ShowRecent());
    NavSettingsCommand = ReactiveCommand.Create(() => ShowSettings());
    OnlineToggleCommand = ReactiveCommand.CreateFromTask(ToggleOnlineAsync);

    _session.WhenAnyValue(s => s.IsAuthenticated)
      .Skip(1)
      .DistinctUntilChanged()
      .ObserveOn(RxApp.MainThreadScheduler)
      .Subscribe(isAuth =>
      {
        if (isAuth)
        {
          ShowHome();
          if (_p2pHost.ShouldBeOnline)
            Task.Run(() => ConnectP2PAsync()).ConfigureAwait(false);
        }
        else
        {
          ShowLogin();
          IsOnline = false;
          Task.Run(() => _p2pHost.DisconnectAsync()).ConfigureAwait(false);
        }
      });

    ShowLogin();
  }

  private async Task ConnectP2PAsync()
  {
    try
    {
      await _p2pHost.ConnectAsync();
      IsOnline = _p2pHost.IsOnline;
    }
    catch (Exception ex)
    {
      Serilog.Log.Warning(ex, "P2P connect failed");
    }
  }

  private async Task ToggleOnlineAsync()
  {
    if (_p2pHost.IsOnline)
    {
      _p2pHost.ShouldBeOnline = false;
      await _p2pHost.DisconnectAsync();
      IsOnline = false;
      _notifications.Info("P2P-доступ к файлам отключён.");
    }
    else
    {
      _p2pHost.ShouldBeOnline = true;
      await ConnectP2PAsync();
      if (IsOnline)
        _notifications.Success("P2P-доступ к файлам включён.");
      else
        _notifications.Error("Не удалось подключиться к signaling-серверу.");
    }
  }

  public void ShowLogin()
  {
    var vm = new LoginViewModel(_authApi, _session, _notifications);
    vm.NavigateToRegister += (_, _) => ShowRegister();
    CurrentPage = vm;
    CurrentNav = "login";
  }

  public void ShowRegister()
  {
    var vm = new RegisterViewModel(_authApi, _notifications);
    vm.NavigateToLogin += (_, _) => ShowLogin();
    vm.RegistrationSuccess += (_, _) => ShowLogin();
    CurrentPage = vm;
    CurrentNav = "register";
  }

  public void ShowHome()
  {
    CurrentPage = new HomeViewModel(_foldersApi, _sharesApi, _session, _notifications);
    CurrentNav = "home";
  }

  public void ShowFolders()
  {
    var vm = new MyFoldersViewModel(_foldersApi, _session, _dialogs, _notifications, _registry, _p2pHost, _signalingApi, _deviceIdentity);
    vm.FolderOpened += (_, folder) => ShowFolderDetail(folder);
    CurrentPage = vm;
    CurrentNav = "folders";
  }

  public void ShowFolderDetail(FolderResponse folder)
  {
    CurrentPage = new FolderDetailViewModel(folder, _permissionsApi, _sharesApi, _signalingApi, _session, _dialogs, _notifications);
    CurrentNav = "folder-detail";
  }

  public void ShowSharedWithMe()
  {
    CurrentPage = new SharedWithMeViewModel(_foldersApi, _session, _notifications);
    CurrentNav = "shared";
  }

  public void ShowPublicShare()
  {
    CurrentPage = new PublicShareViewModel(_sharesApi, _notifications);
    CurrentNav = "public-share";
  }

  public void ShowLinks()
  {
    CurrentPage = new LinksViewModel(_sharesApi, _clipboard, _notifications);
    CurrentNav = "links";
  }

  public void ShowRecent()
  {
    var vm = new RecentViewModel(_foldersApi, _notifications);
    vm.FolderOpened += (_, folder) => ShowFolderDetail(folder);
    CurrentPage = vm;
    CurrentNav = "recent";
  }

  public void ShowSettings()
  {
    CurrentPage = new SettingsViewModel(_session, _signalingApi, _deviceIdentity, _notifications);
    CurrentNav = "settings";
  }

  private async Task LogoutAsync()
  {
    try { await _authApi.LogoutAsync(null); }
    catch { /* best-effort */ }
    finally
    {
      await _session.SignOutAsync();
    }
  }
}
