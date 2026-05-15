using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class HomeViewModel : ViewModelBase
{
    private readonly IFoldersApi _foldersApi;
    private readonly ISharesApi _sharesApi;
    private readonly IAuthSession _session;
    private readonly INotificationService _notifications;

    private bool _isLoading;
    private int _ownedCount;
    private int _sharedCount;
    private int _activeLinksCount;
    private int _totalCount;

    public bool IsLoading { get => _isLoading; private set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
    public int OwnedCount { get => _ownedCount; private set => this.RaiseAndSetIfChanged(ref _ownedCount, value); }
    public int SharedCount { get => _sharedCount; private set => this.RaiseAndSetIfChanged(ref _sharedCount, value); }
    public int ActiveLinksCount { get => _activeLinksCount; private set => this.RaiseAndSetIfChanged(ref _activeLinksCount, value); }
    public int TotalCount { get => _totalCount; private set => this.RaiseAndSetIfChanged(ref _totalCount, value); }

    public ObservableCollection<FolderResponse> RecentFolders { get; } = [];

    public ReactiveCommand<Unit, Unit> LoadCommand { get; }

    public HomeViewModel(IFoldersApi foldersApi, ISharesApi sharesApi, IAuthSession session, INotificationService notifications)
    {
        _foldersApi = foldersApi;
        _sharesApi = sharesApi;
        _session = session;
        _notifications = notifications;

        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);

        this.WhenActivated((CompositeDisposable _) => LoadCommand.Execute().Subscribe());
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        try
        {
            var foldersTask = _foldersApi.GetFoldersAsync(ct);
            var sharesTask = _sharesApi.ListAllSharesAsync(activeOnly: false, ct: ct);
            await Task.WhenAll(foldersTask, sharesTask);

            var folders = await foldersTask;
            var shares = await sharesTask;
            var userId = _session.CurrentUserId;

            OwnedCount = folders.Count(f => f.OwnerId == userId);
            SharedCount = folders.Count(f => f.OwnerId != userId);
            ActiveLinksCount = shares.Count(s => !s.Revoked);
            TotalCount = folders.Count;

            RecentFolders.Clear();
            var recent = folders
                .OrderByDescending(f => f.UpdatedAt ?? DateTime.MinValue)
                .Take(6);
            foreach (var f in recent)
                RecentFolders.Add(f);
        }
        catch
        {
            _notifications.Error("Не удалось загрузить данные.");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
