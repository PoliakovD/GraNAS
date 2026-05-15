using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Sharing;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class LinksViewModel : ViewModelBase
{
    private readonly ISharesApi _sharesApi;
    private readonly IClipboardService _clipboard;
    private readonly INotificationService _notifications;
    private bool _isLoading;

    public bool IsLoading { get => _isLoading; private set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
    public ObservableCollection<ShareLinkOwnerResponse> Links { get; } = [];

    public ReactiveCommand<Unit, Unit> LoadCommand { get; }
    public ReactiveCommand<ShareLinkOwnerResponse, Unit> CopyCommand { get; }
    public ReactiveCommand<ShareLinkOwnerResponse, Unit> RevokeCommand { get; }

    public LinksViewModel(ISharesApi sharesApi, IClipboardService clipboard, INotificationService notifications)
    {
        _sharesApi = sharesApi;
        _clipboard = clipboard;
        _notifications = notifications;

        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        CopyCommand = ReactiveCommand.CreateFromTask<ShareLinkOwnerResponse>(CopyAsync);
        RevokeCommand = ReactiveCommand.CreateFromTask<ShareLinkOwnerResponse>(RevokeAsync);

        this.WhenActivated((CompositeDisposable _) => LoadCommand.Execute().Subscribe());
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        try
        {
            var links = await _sharesApi.ListAllSharesAsync(activeOnly: false, ct: ct);
            Links.Clear();
            foreach (var l in links)
                Links.Add(l);
        }
        catch
        {
            _notifications.Error("Не удалось загрузить ссылки.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CopyAsync(ShareLinkOwnerResponse link, CancellationToken _)
    {
        if (string.IsNullOrEmpty(link.ShareUrl)) return;
        await _clipboard.CopyAsync(link.ShareUrl);
        _notifications.Success("Ссылка скопирована.");
    }

    private async Task RevokeAsync(ShareLinkOwnerResponse link, CancellationToken ct)
    {
        try
        {
            await _sharesApi.RevokeShareAsync(link.Id, ct);
            Links.Remove(link);
            _notifications.Success("Ссылка отозвана.");
        }
        catch
        {
            _notifications.Error("Не удалось отозвать ссылку.");
        }
    }
}
