using System.Collections.ObjectModel;
using GraNAS.Desktop.App.Models;

namespace GraNAS.Desktop.App.Services;

public interface IBackendNotificationService
{
    ObservableCollection<BackendNotificationVm> Notifications { get; }
    int UnreadCount { get; }
    event EventHandler? UnreadCountChanged;

    Task ConnectAsync(string accessToken, CancellationToken ct = default);
    Task DisconnectAsync();
    Task MarkReadAsync(Guid id);
    Task LoadHistoryAsync();
}
