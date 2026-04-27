namespace GraNAS.Desktop.App.Services;

public interface INotificationService
{
  void Success(string message, string? title = null);
  void Error(string message, string? title = null);
  void Info(string message, string? title = null);
}
