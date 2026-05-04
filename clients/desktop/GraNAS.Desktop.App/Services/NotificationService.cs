using Avalonia.Controls.Notifications;

namespace GraNAS.Desktop.App.Services;

public class NotificationService : INotificationService
{
  private WindowNotificationManager? _manager;

  public void SetManager(WindowNotificationManager manager) => _manager = manager;

  public void Success(string message, string? title = null)
    => Show(message, title ?? "Успешно", NotificationType.Success);

  public void Error(string message, string? title = null)
    => Show(message, title ?? "Ошибка", NotificationType.Error);

  public void Info(string message, string? title = null)
    => Show(message, title ?? "Информация", NotificationType.Information);

  private void Show(string message, string title, NotificationType type)
  {
    if (_manager is null) return;
    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
      _manager.Show(new Notification(title, message, type, expiration: TimeSpan.FromSeconds(4))));
  }
}
