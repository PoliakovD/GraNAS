using System.Collections.ObjectModel;
using System.Reactive;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class PermissionsListViewModel : ViewModelBase
{
  private readonly IPermissionsApi _permissionsApi;
  private readonly Guid _folderId;
  private readonly IDialogService _dialogs;
  private readonly INotificationService _notifications;

  private ObservableCollection<PermissionResponse> _permissions = [];
  private bool _isLoading;

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

  public ReactiveCommand<Unit, Unit> LoadCommand { get; }
  public ReactiveCommand<PermissionResponse, Unit> RevokeCommand { get; }
  public ReactiveCommand<Unit, Unit> GrantCommand { get; }

  public PermissionsListViewModel(
    IPermissionsApi permissionsApi,
    Guid folderId,
    IDialogService dialogs,
    INotificationService notifications)
  {
    _permissionsApi = permissionsApi;
    _folderId = folderId;
    _dialogs = dialogs;
    _notifications = notifications;

    LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
    RevokeCommand = ReactiveCommand.CreateFromTask<PermissionResponse>(RevokeAsync);
    GrantCommand = ReactiveCommand.CreateFromTask(GrantAsync);

    this.WhenActivated((System.Reactive.Disposables.CompositeDisposable _) => LoadCommand.Execute().Subscribe());
  }

  private async Task LoadAsync()
  {
    IsLoading = true;
    try
    {
      var list = await _permissionsApi.GetPermissionsAsync(_folderId);
      Permissions = new ObservableCollection<PermissionResponse>(list);
    }
    catch
    {
      _notifications.Error("Не удалось загрузить список прав.");
    }
    finally
    {
      IsLoading = false;
    }
  }

  private async Task GrantAsync()
  {
    var result = await _dialogs.ShowGrantPermissionAsync();
    if (result is null) return;

    try
    {
      await _permissionsApi.GrantAsync(_folderId,
        new GrantPermissionRequest { Email = result.Value.Email, AccessLevel = result.Value.Level });
      _notifications.Success($"Доступ выдан пользователю {result.Value.Email}.");
      await LoadAsync();
    }
    catch (ApiException ex) when (ex.StatusCode == 404 && ex.Error?.Error == "user_not_found")
    {
      _notifications.Error($"Пользователь {result.Value.Email} не найден.");
    }
    catch (ApiException ex)
    {
      _notifications.Error(ex.Error?.ErrorDescription ?? ex.Message);
    }
    catch
    {
      _notifications.Error("Не удалось выдать доступ.");
    }
  }

  private async Task RevokeAsync(PermissionResponse perm)
  {
    try
    {
      await _permissionsApi.RevokeAsync(_folderId, perm.UserId);
      Permissions.Remove(perm);
      _notifications.Success("Доступ отозван.");
    }
    catch (ApiException ex)
    {
      _notifications.Error(ex.Error?.ErrorDescription ?? ex.Message);
    }
    catch
    {
      _notifications.Error("Не удалось отозвать доступ.");
    }
  }
}
