using System.Collections.ObjectModel;
using System.Reactive;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.Folders;
using GraNAS.Desktop.App.Services.P2P;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

/// <summary>
/// ViewModel страницы «Мои папки». Отображает дерево папок пользователя,
/// управляет созданием/удалением и P2P-привязкой папок к локальным путям.
/// </summary>
public class MyFoldersViewModel : ViewModelBase
{
  private readonly IFoldersApi _foldersApi;
  private readonly IAuthSession _session;
  private readonly IDialogService _dialogs;
  private readonly INotificationService _notifications;
  private readonly IFolderShareRegistry _registry;
  private readonly IP2PHost _p2pHost;
  private readonly ISignalingApi _signalingApi;
  private readonly IDeviceIdentity _deviceIdentity;

  private ObservableCollection<FolderNode> _roots = [];
  private FolderNode? _selectedNode;
  private bool _isLoading;

  /// <summary>Корневые папки дерева (папки без родителя).</summary>
  public ObservableCollection<FolderNode> Roots
  {
    get => _roots;
    private set => this.RaiseAndSetIfChanged(ref _roots, value);
  }

  /// <summary>Текущий выделенный узел дерева папок.</summary>
  public FolderNode? SelectedNode
  {
    get => _selectedNode;
    set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
  }

  /// <summary><c>true</c>, пока загружается список папок с сервера.</summary>
  public bool IsLoading
  {
    get => _isLoading;
    set => this.RaiseAndSetIfChanged(ref _isLoading, value);
  }

  /// <summary>Загрузить/перезагрузить список папок с сервера.</summary>
  public ReactiveCommand<Unit, Unit> LoadCommand { get; }
  /// <summary>Создать корневую папку.</summary>
  public ReactiveCommand<Unit, Unit> CreateRootCommand { get; }
  /// <summary>Удалить папку (и все вложенные — по каскаду на сервере).</summary>
  public ReactiveCommand<FolderNode, Unit> DeleteCommand { get; }
  /// <summary>Открыть детальный вид папки (навигация к <c>FolderDetailViewModel</c>).</summary>
  public ReactiveCommand<FolderNode, Unit> OpenCommand { get; }
  /// <summary>Создать вложенную папку.</summary>
  public ReactiveCommand<FolderNode, Unit> CreateSubfolderCommand { get; }
  /// <summary>Привязать локальную папку на диске для P2P-шаринга.</summary>
  public ReactiveCommand<FolderNode, Unit> BindLocalFolderCommand { get; }

  /// <summary>Вызывается при открытии папки пользователем; передаёт объект <see cref="FolderResponse"/>.</summary>
  public event EventHandler<FolderResponse>? FolderOpened;

  public MyFoldersViewModel(
    IFoldersApi foldersApi,
    IAuthSession session,
    IDialogService dialogs,
    INotificationService notifications,
    IFolderShareRegistry registry,
    IP2PHost p2pHost,
    ISignalingApi signalingApi,
    IDeviceIdentity deviceIdentity)
  {
    _foldersApi = foldersApi;
    _session = session;
    _dialogs = dialogs;
    _notifications = notifications;
    _registry = registry;
    _p2pHost = p2pHost;
    _signalingApi = signalingApi;
    _deviceIdentity = deviceIdentity;

    LoadCommand = ReactiveCommand.CreateFromTask(LoadFoldersAsync);
    CreateRootCommand = ReactiveCommand.CreateFromTask(() => CreateFolderAsync(null));
    DeleteCommand = ReactiveCommand.CreateFromTask<FolderNode>(DeleteFolderAsync);
    OpenCommand = ReactiveCommand.Create<FolderNode>(n => FolderOpened?.Invoke(this, n.Folder));
    CreateSubfolderCommand = ReactiveCommand.CreateFromTask<FolderNode>(n => CreateFolderAsync(n.Folder.Id));
    BindLocalFolderCommand = ReactiveCommand.CreateFromTask<FolderNode>(BindLocalFolderAsync);

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

  /// <summary>
  /// Связывает папку GraNAS с локальной директорией для P2P-шаринга.
  /// Поток: выбор пути → Claim на сервере → при конфликте диалог подтверждения → re-Claim с <c>force=true</c>
  /// → сохранение пути в <see cref="IFolderShareRegistry"/> → <c>JoinFolderAsync</c> в хабе.
  /// </summary>
  private async Task BindLocalFolderAsync(FolderNode node)
  {
    var path = await _dialogs.ShowFolderPickerAsync("Выберите локальную папку для P2P-шаринга");
    if (path is null) return;

    // Привязываем папку к этому устройству на сервере (device-folder binding)
    var conflict = await _signalingApi.ClaimFolderAsync(_deviceIdentity.DeviceId, node.Folder.Id);
    if (conflict is not null)
    {
      var confirmed = await _dialogs.ShowConfirmAsync(
        "Переназначить устройство?",
        $"Папка «{node.Folder.Name}» уже привязана к «{conflict.DeviceName}». Переназначить на это устройство?",
        "Переназначить");
      if (!confirmed) return;

      await _signalingApi.ClaimFolderAsync(_deviceIdentity.DeviceId, node.Folder.Id, force: true);
    }

    _registry.SetLocalPath(node.Folder.Id, path);
    await _p2pHost.JoinFolderAsync(node.Folder.Id);
    _notifications.Success($"Папка «{node.Folder.Name}» привязана к {path}. P2P-доступ активен.");
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
