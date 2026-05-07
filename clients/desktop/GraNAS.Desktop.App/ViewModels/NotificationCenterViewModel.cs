using System.Collections.ObjectModel;
using System.Reactive;
using GraNAS.Desktop.App.Models;
using GraNAS.Desktop.App.Services;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class NotificationCenterViewModel : ViewModelBase
{
    private readonly IBackendNotificationService _notifService;
    private int _unreadCount;

    public ObservableCollection<BackendNotificationVm> Notifications =>
        _notifService.Notifications;

    public int UnreadCount
    {
        get => _unreadCount;
        private set => this.RaiseAndSetIfChanged(ref _unreadCount, value);
    }

    public ReactiveCommand<BackendNotificationVm, Unit> MarkReadCommand { get; }
    public ReactiveCommand<Unit, Unit> MarkAllReadCommand { get; }

    public NotificationCenterViewModel(IBackendNotificationService notifService)
    {
        _notifService = notifService;
        _unreadCount = _notifService.UnreadCount;

        _notifService.UnreadCountChanged += (_, _) =>
        {
            UnreadCount = _notifService.UnreadCount;
        };

        MarkReadCommand = ReactiveCommand.CreateFromTask<BackendNotificationVm>(async vm =>
        {
            if (!vm.IsRead)
                await _notifService.MarkReadAsync(vm.Id);
        });

        MarkAllReadCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            foreach (var n in Notifications.Where(n => !n.IsRead).ToList())
                await _notifService.MarkReadAsync(n.Id);
        });
    }
}
