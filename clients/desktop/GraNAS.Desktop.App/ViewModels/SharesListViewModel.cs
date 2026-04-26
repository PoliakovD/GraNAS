using System.Collections.ObjectModel;
using System.Reactive;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Sharing;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class SharesListViewModel : ViewModelBase
{
  private readonly ISharesApi _sharesApi;
  private readonly Guid _folderId;

  private ObservableCollection<ShareLinkResponse> _shares = [];
  private bool _isLoading;
  private string? _errorMessage;

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

  public string? ErrorMessage
  {
    get => _errorMessage;
    set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
  }

  public ReactiveCommand<Unit, Unit> LoadCommand { get; }
  public ReactiveCommand<ShareLinkResponse, Unit> RevokeCommand { get; }

  /// Raised with newly created token — caller shows it once in a dialog
  public event EventHandler<string>? ShareCreated;
  public event EventHandler? CreateShareRequested;

  public SharesListViewModel(ISharesApi sharesApi, Guid folderId)
  {
    _sharesApi = sharesApi;
    _folderId = folderId;

    LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
    RevokeCommand = ReactiveCommand.CreateFromTask<ShareLinkResponse>(RevokeAsync);

    this.WhenActivated((System.Reactive.Disposables.CompositeDisposable _) => LoadCommand.Execute().Subscribe());
  }

  private async Task LoadAsync()
  {
    ErrorMessage = null;
    IsLoading = true;
    try
    {
      var list = await _sharesApi.GetSharesAsync(_folderId);
      Shares = new ObservableCollection<ShareLinkResponse>(list);
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

  private async Task RevokeAsync(ShareLinkResponse share)
  {
    try
    {
      await _sharesApi.RevokeShareAsync(share.Id);
      Shares.Remove(share);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task CreateShareAsync(DateTime expiresAt)
  {
    try
    {
      var result = await _sharesApi.CreateShareAsync(_folderId,
        new CreateShareRequest { ExpiresAt = expiresAt });
      ShareCreated?.Invoke(this, result.Token);
      await LoadAsync();
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      throw;
    }
  }
}
