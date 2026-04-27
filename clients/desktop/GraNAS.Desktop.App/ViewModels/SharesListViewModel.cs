using System.Collections.ObjectModel;
using System.Reactive;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Sharing;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class SharesListViewModel : ViewModelBase
{
  private readonly ISharesApi _sharesApi;
  private readonly Guid _folderId;
  private readonly IDialogService _dialogs;
  private readonly INotificationService _notifications;

  private ObservableCollection<ShareLinkResponse> _shares = [];
  private bool _isLoading;

  public ObservableCollection<ShareLinkResponse> Shares
  {
    get => _shares;
    private set => this.RaiseAndSetIfChanged(ref _shares, value);
  }

  public bool IsLoading
  {
    get => _isLoading;
    set => this.RaiseAndSetIfChanged(ref _isLoading, value);
  }

  public ReactiveCommand<Unit, Unit> LoadCommand { get; }
  public ReactiveCommand<ShareLinkResponse, Unit> RevokeCommand { get; }
  public ReactiveCommand<Unit, Unit> CreateCommand { get; }

  public SharesListViewModel(
    ISharesApi sharesApi,
    Guid folderId,
    IDialogService dialogs,
    INotificationService notifications)
  {
    _sharesApi = sharesApi;
    _folderId = folderId;
    _dialogs = dialogs;
    _notifications = notifications;

    LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
    RevokeCommand = ReactiveCommand.CreateFromTask<ShareLinkResponse>(RevokeAsync);
    CreateCommand = ReactiveCommand.CreateFromTask(CreateAsync);

    this.WhenActivated((System.Reactive.Disposables.CompositeDisposable _) => LoadCommand.Execute().Subscribe());
  }

  private async Task LoadAsync()
  {
    IsLoading = true;
    try
    {
      var list = await _sharesApi.GetSharesAsync(_folderId);
      Shares = new ObservableCollection<ShareLinkResponse>(list);
    }
    catch
    {
      _notifications.Error("Не удалось загрузить share-ссылки.");
    }
    finally
    {
      IsLoading = false;
    }
  }

  private async Task CreateAsync()
  {
    var expiresAt = await _dialogs.ShowCreateShareAsync();
    if (expiresAt is null) return;

    try
    {
      var result = await _sharesApi.CreateShareAsync(_folderId,
        new CreateShareRequest { ExpiresAt = expiresAt.Value });

      // Show token once — user must copy it now
      await _dialogs.ShowShareCreatedAsync(result.Token);
      await LoadAsync();
    }
    catch (ApiException ex)
    {
      _notifications.Error(ex.Error?.ErrorDescription ?? ex.Message);
    }
    catch
    {
      _notifications.Error("Не удалось создать share-ссылку.");
    }
  }

  private async Task RevokeAsync(ShareLinkResponse share)
  {
    try
    {
      await _sharesApi.RevokeShareAsync(share.Id);
      Shares.Remove(share);
      _notifications.Success("Share-ссылка отозвана.");
    }
    catch (ApiException ex)
    {
      _notifications.Error(ex.Error?.ErrorDescription ?? ex.Message);
    }
    catch
    {
      _notifications.Error("Не удалось отозвать ссылку.");
    }
  }
}
