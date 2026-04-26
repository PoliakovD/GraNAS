using System.Collections.ObjectModel;
using System.Reactive;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.Folders;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class MyFoldersViewModel : ViewModelBase
{
  private readonly IFoldersApi _foldersApi;
  private readonly IAuthSession _session;

  private ObservableCollection<FolderNode> _roots = [];
  private FolderNode? _selectedNode;
  private bool _isLoading;
  private string? _errorMessage;

  public ObservableCollection<FolderNode> Roots
  {
    get => _roots;
    private set => this.RaiseAndSetIfChanged(ref _roots, value);
  }

  public FolderNode? SelectedNode
  {
    get => _selectedNode;
    set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
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
  public ReactiveCommand<Unit, Unit> CreateRootCommand { get; }
  public ReactiveCommand<FolderNode, Unit> DeleteCommand { get; }
  public ReactiveCommand<FolderNode, Unit> OpenCommand { get; }

  public event EventHandler<FolderResponse>? FolderOpened;
  public event EventHandler? CreateFolderRequested;

  public MyFoldersViewModel(IFoldersApi foldersApi, IAuthSession session)
  {
    _foldersApi = foldersApi;
    _session = session;

    LoadCommand = ReactiveCommand.CreateFromTask(LoadFoldersAsync);
    CreateRootCommand = ReactiveCommand.Create(() => CreateFolderRequested?.Invoke(this, EventArgs.Empty));
    DeleteCommand = ReactiveCommand.CreateFromTask<FolderNode>(DeleteFolderAsync);
    OpenCommand = ReactiveCommand.Create<FolderNode>(n => FolderOpened?.Invoke(this, n.Folder));

    this.WhenActivated((System.Reactive.Disposables.CompositeDisposable _) => LoadCommand.Execute().Subscribe());
  }

  private async Task LoadFoldersAsync()
  {
    ErrorMessage = null;
    IsLoading = true;
    try
    {
      var all = await _foldersApi.GetFoldersAsync();
      var tree = FolderTreeBuilder.Build(all, _session.CurrentUserId);
      Roots = new ObservableCollection<FolderNode>(tree);
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

  private async Task DeleteFolderAsync(FolderNode node)
  {
    try
    {
      await _foldersApi.DeleteFolderAsync(node.Folder.Id);
      await LoadFoldersAsync();
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task CreateSubfolderAsync(Guid? parentId, string name)
  {
    try
    {
      await _foldersApi.CreateFolderAsync(new CreateFolderRequest { Name = name, ParentFolderId = parentId });
      await LoadFoldersAsync();
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }
}
