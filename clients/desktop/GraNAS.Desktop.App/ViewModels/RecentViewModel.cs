using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class RecentViewModel : ViewModelBase
{
    private readonly IFoldersApi _foldersApi;
    private readonly INotificationService _notifications;
    private bool _isLoading;

    public bool IsLoading { get => _isLoading; private set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
    public ObservableCollection<FolderResponse> RecentFolders { get; } = [];

    public ReactiveCommand<Unit, Unit> LoadCommand { get; }
    public ReactiveCommand<FolderResponse, Unit> OpenCommand { get; }

    public event EventHandler<FolderResponse>? FolderOpened;

    public RecentViewModel(IFoldersApi foldersApi, INotificationService notifications)
    {
        _foldersApi = foldersApi;
        _notifications = notifications;

        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        OpenCommand = ReactiveCommand.Create<FolderResponse>(f => FolderOpened?.Invoke(this, f));

        this.WhenActivated((CompositeDisposable _) => LoadCommand.Execute().Subscribe());
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        try
        {
            var folders = await _foldersApi.GetFoldersAsync(ct);
            var recent = folders
                .Where(f => f.LastAccessedAt.HasValue || f.UpdatedAt.HasValue)
                .OrderByDescending(f => f.LastAccessedAt ?? f.UpdatedAt ?? DateTime.MinValue)
                .Take(12);

            RecentFolders.Clear();
            foreach (var f in recent)
                RecentFolders.Add(f);
        }
        catch
        {
            _notifications.Error("Не удалось загрузить папки.");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
