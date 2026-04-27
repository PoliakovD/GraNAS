using System.Collections.ObjectModel;
using System.Reactive;
using GraNAS.Desktop.App.Services;
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
  private readonly IDialogService _dialogs;
  private readonly INotificationService _notifications;

  private ObservableCollection<FolderNode> _roots = [];
  private FolderNode? _selectedNode;
  private bool _isLoading;

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

  public ReactiveCommand<Unit, Unit> LoadCommand { get; }
  public ReactiveCommand<Unit, Unit> CreateRootCommand { get; }
  public ReactiveCommand<FolderNode, Unit> DeleteCommand { get; }
  public ReactiveCommand<FolderNode, Unit> OpenCommand { get; }
  public ReactiveCommand<FolderNode, Unit> CreateSubfolderCommand { get; }

  public event EventHandler<FolderResponse>? FolderOpened;

  public MyFoldersViewModel(
    IFoldersApi foldersApi,
    IAuthSession session,
    IDialogService dialogs,
    INotificationService notifications)
  {
    _foldersApi = foldersApi;
    _session = session;
    _dialogs = dialogs;
    _notifications = notifications;

    LoadCommand = ReactiveCommand.CreateFromTask(LoadFoldersAsync);
    CreateRootCommand = ReactiveCommand.CreateFromTask(() => CreateFolderAsync(null));
    DeleteCommand = ReactiveCommand.CreateFromTask<FolderNode>(DeleteFolderAsync);
    OpenCommand = ReactiveCommand.Create<FolderNode>(n => FolderOpened?.Invoke(this, n.Folder));
    CreateSubfolderCommand = ReactiveCommand.CreateFromTask<FolderNode>(n => CreateFolderAsync(n.Folder.Id));

    this.WhenActivated((System.Reactive.Disposables.CompositeDisposable _) => LoadCommand.Execute().Subscribe());
  }

  private async Task LoadFoldersAsync()
  {
    IsLoading = true;
    try
    {
      var all = await _foldersApi.GetFoldersAsync();
      var tree = FolderTreeBuilder.Build(all, _session.CurrentUserId);
      Roots = new ObservableCollection<FolderNode>(tree);
    }
    catch
    {
      _notifications.Error("Не удалось загрузить папки. Проверьте соединение.");
    }
    finally
    {
      IsLoading = false;
    }
  }

  private async Task CreateFolderAsync(Guid? parentId)
  {
    var name = await _dialogs.ShowCreateFolderAsync();
    if (name is null) return;

    try
    {
      await _foldersApi.CreateFolderAsync(new CreateFolderRequest { Name = name, ParentFolderId = parentId });
      _notifications.Success($"Папка «{name}» создана.");
      await LoadFoldersAsync();
    }
    catch (ApiException ex)
    {
      _notifications.Error(ex.Error?.ErrorDescription ?? ex.Message);
    }
    catch
    {
      _notifications.Error("Не удалось создать папку.");
    }
  }

  private async Task DeleteFolderAsync(FolderNode node)
  {
    try
    {
      await _foldersApi.DeleteFolderAsync(node.Folder.Id);
      _notifications.Success($"Папка «{node.Folder.Name}» удалена.");
      await LoadFoldersAsync();
    }
    catch (ApiException ex)
    {
      _notifications.Error(ex.Error?.ErrorDescription ?? ex.Message);
    }
    catch
    {
      _notifications.Error("Не удалось удалить папку.");
    }
  }
}
