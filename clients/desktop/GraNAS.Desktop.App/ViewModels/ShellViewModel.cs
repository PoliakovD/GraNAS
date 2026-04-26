using System.Reactive;
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
    ISharesApi sharesApi)
  {
    _session = session;
    _authApi = authApi;
    _foldersApi = foldersApi;
    _permissionsApi = permissionsApi;
    _sharesApi = sharesApi;

    LogoutCommand = ReactiveCommand.CreateFromTask(LogoutAsync);
    NavFoldersCommand = ReactiveCommand.Create(() => ShowFolders());
    NavSharedCommand = ReactiveCommand.Create(() => ShowSharedWithMe());
    NavPublicShareCommand = ReactiveCommand.Create(() => ShowPublicShare());

    ShowFolders();
  }

  public void ShowLogin()
  {
    var vm = new LoginViewModel(_authApi, _session);
    vm.NavigateToRegister += (_, _) => ShowRegister();
    CurrentPage = vm;
    CurrentNav = "login";
  }

  public void ShowRegister()
  {
    var vm = new RegisterViewModel(_authApi);
    vm.NavigateToLogin += (_, _) => ShowLogin();
    vm.RegistrationSuccess += (_, _) => ShowLogin();
    CurrentPage = vm;
    CurrentNav = "register";
  }

  public void ShowFolders()
  {
    var vm = new MyFoldersViewModel(_foldersApi, _session);
    vm.FolderOpened += (_, folder) => ShowFolderDetail(folder);
    CurrentPage = vm;
    CurrentNav = "folders";
  }

  public void ShowFolderDetail(FolderResponse folder)
  {
    CurrentPage = new FolderDetailViewModel(folder, _permissionsApi, _sharesApi);
    CurrentNav = "folder-detail";
  }

  public void ShowSharedWithMe()
  {
    CurrentPage = new SharedWithMeViewModel(_foldersApi, _session);
    CurrentNav = "shared";
  }

  public void ShowPublicShare()
  {
    CurrentPage = new PublicShareViewModel(_sharesApi);
    CurrentNav = "public-share";
  }

  private async Task LogoutAsync()
  {
    try
    {
      var token = _session.AccessToken;
      await _authApi.LogoutAsync(null);
    }
    catch { /* best-effort */ }
    finally
    {
      await _session.SignOutAsync();
      ShowLogin();
    }
  }
}
