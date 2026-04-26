using System.Collections.ObjectModel;
using System.Reactive;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.Folders;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class SharedWithMeViewModel : ViewModelBase
{
  private readonly IFoldersApi _foldersApi;
  private readonly IAuthSession _session;

  private ObservableCollection<FolderResponse> _folders = [];
  private bool _isLoading;
  private string? _errorMessage;

  public ObservableCollection<FolderResponse> Folders
  {
    get => _folders;
    private set => this.RaiseAndSetIfChanged(ref _folders, value);
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

  public SharedWithMeViewModel(IFoldersApi foldersApi, IAuthSession session)
  {
    _foldersApi = foldersApi;
    _session = session;

    LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
    this.WhenActivated((System.Reactive.Disposables.CompositeDisposable _) => LoadCommand.Execute().Subscribe());
  }

  private async Task LoadAsync()
  {
    ErrorMessage = null;
    IsLoading = true;
    try
    {
      var all = await _foldersApi.GetFoldersAsync();
      var shared = FolderTreeBuilder.GetSharedWithMe(all, _session.CurrentUserId);
      Folders = new ObservableCollection<FolderResponse>(shared);
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
}
