using System.Collections.ObjectModel;
using GraNAS.Desktop.App.Models;

namespace GraNAS.Desktop.App.Services;

/// <summary>Сервис получения и управления backend-уведомлениями в реальном времени через SignalR.</summary>
public interface IBackendNotificationService
{
    /// <summary>Реактивная коллекция всех полученных уведомлений (в порядке: новые первые).</summary>
    ObservableCollection<BackendNotificationVm> Notifications { get; }
    /// <summary>Количество непрочитанных уведомлений.</summary>
    int UnreadCount { get; }
    /// <summary>Вызывается при изменении количества непрочитанных уведомлений.</summary>
    event EventHandler? UnreadCountChanged;

    /// <summary>Подключается к SignalR-хабу уведомлений и подписывается на события.</summary>
    Task ConnectAsync(string accessToken, CancellationToken ct = default);
    /// <summary>Отключается от хаба уведомлений.</summary>
    Task DisconnectAsync();
    /// <summary>Отмечает уведомление как прочитанное через хаб (или REST при офлайн).</summary>
    Task MarkReadAsync(Guid id);
    /// <summary>Загружает историю уведомлений из REST API и заполняет <see cref="Notifications"/>.</summary>
    Task LoadHistoryAsync();
}
