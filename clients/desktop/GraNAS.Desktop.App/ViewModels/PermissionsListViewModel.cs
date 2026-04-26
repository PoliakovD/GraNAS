using System.Collections.ObjectModel;
using System.Reactive;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class PermissionsListViewModel : ViewModelBase
{
  private readonly IPermissionsApi _permissionsApi;
  private readonly Guid _folderId;

  private ObservableCollection<PermissionResponse> _permissions = [];
  private bool _isLoading;
  private string? _errorMessage;

  public ObservableCollection<PermissionResponse> Permissions
  {
    get => _permissions;
    private set => this.RaiseAndSetIfChanged(ref _permissions, value);
  }

  public bool IsLoading
  {
    get => _isLoading;
    set => this.RaiseAndSetIfChanged(ref _isLoading, value);
  }

  public string? ErrorMessage
  {
    get => _errorMessage;
    set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
  }

  public ReactiveCommand<Unit, Unit> LoadCommand { get; }
  public ReactiveCommand<PermissionResponse, Unit> RevokeCommand { get; }

  public event EventHandler? GrantRequested;

  public PermissionsListViewModel(IPermissionsApi permissionsApi, Guid folderId)
  {
    _permissionsApi = permissionsApi;
    _folderId = folderId;

    LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
    RevokeCommand = ReactiveCommand.CreateFromTask<PermissionResponse>(RevokeAsync);

    this.WhenActivated((System.Reactive.Disposables.CompositeDisposable _) => LoadCommand.Execute().Subscribe());
  }

  private async Task LoadAsync()
  {
    ErrorMessage = null;
    IsLoading = true;
    try
    {
      var list = await _permissionsApi.GetPermissionsAsync(_folderId);
      Permissions = new ObservableCollection<PermissionResponse>(list);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }

  private async Task RevokeAsync(PermissionResponse perm)
  {
    try
    {
      await _permissionsApi.RevokeAsync(_folderId, perm.UserId);
      Permissions.Remove(perm);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task GrantPermissionAsync(string email, AccessLevel level)
  {
    try
    {
      var result = await _permissionsApi.GrantAsync(_folderId,
        new GrantPermissionRequest { Email = email, AccessLevel = level });
      // Refresh list
      await LoadAsync();
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      throw;
    }
  }
}
