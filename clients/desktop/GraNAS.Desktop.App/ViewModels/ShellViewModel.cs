using System.Reactive;
using System.Reactive.Linq;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class ShellViewModel : ViewModelBase
{
  private readonly IAuthSession _session;
  private readonly IAuthApi _authApi;
  private readonly IFoldersApi _foldersApi;
  private readonly IPermissionsApi _permissionsApi;
  private readonly ISharesApi _sharesApi;
  private readonly IDialogService _dialogs;
  private readonly INotificationService _notifications;

  private ViewModelBase? _currentPage;
  private string _currentNav = "folders";

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

  public IAuthSession Session => _session;

  public ReactiveCommand<Unit, Unit> LogoutCommand { get; }
  public ReactiveCommand<Unit, Unit> NavFoldersCommand { get; }
  public ReactiveCommand<Unit, Unit> NavSharedCommand { get; }
  public ReactiveCommand<Unit, Unit> NavPublicShareCommand { get; }

  public ShellViewModel(
    IAuthSession session,
    IAuthApi authApi,
    IFoldersApi foldersApi,
    IPermissionsApi permissionsApi,
    ISharesApi sharesApi,
    IDialogService dialogs,
    INotificationService notifications)
  {
    _session = session;
    _authApi = authApi;
    _foldersApi = foldersApi;
    _permissionsApi = permissionsApi;
    _sharesApi = sharesApi;
    _dialogs = dialogs;
    _notifications = notifications;

    LogoutCommand = ReactiveCommand.CreateFromTask(LogoutAsync);
    NavFoldersCommand = ReactiveCommand.Create(() => ShowFolders());
    NavSharedCommand = ReactiveCommand.Create(() => ShowSharedWithMe());
    NavPublicShareCommand = ReactiveCommand.Create(() => ShowPublicShare());

    // React to auth state changes: login/restore → Folders; logout/expire → Login.
    // Skip(1) ignores the initial false emission at subscription time.
    _session.WhenAnyValue(s => s.IsAuthenticated)
      .Skip(1)
      .DistinctUntilChanged()
      .ObserveOn(RxApp.MainThreadScheduler)
      .Subscribe(isAuth =>
      {
        if (isAuth) ShowFolders();
        else ShowLogin();
      });

    // Default state: Login. window.Opened triggers TryRestoreAsync which updates IsAuthenticated.
    ShowLogin();
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

  public void ShowFolders()
  {
    var vm = new MyFoldersViewModel(_foldersApi, _session, _dialogs, _notifications);
    vm.FolderOpened += (_, folder) => ShowFolderDetail(folder);
    CurrentPage = vm;
    CurrentNav = "folders";
  }

  public void ShowFolderDetail(FolderResponse folder)
  {
    CurrentPage = new FolderDetailViewModel(folder, _permissionsApi, _sharesApi, _dialogs, _notifications);
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

  private async Task LogoutAsync()
  {
    try { await _authApi.LogoutAsync(null); }
    catch { /* best-effort */ }
    finally
    {
      // SignOutAsync sets IsAuthenticated = false → WhenAnyValue subscription calls ShowLogin()
      await _session.SignOutAsync();
    }
  }
}
