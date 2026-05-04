using Avalonia.Controls.Notifications;
using Avalonia.ReactiveUI;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Desktop.App.Views;

public partial class ShellWindow : ReactiveWindow<ShellViewModel>
{
  public ShellWindow()
  {
    InitializeComponent();
  }

  protected override void OnOpened(EventArgs e)
  {
    base.OnOpened(e);

    // Wire up toast notifications to this window
    var notifSvc = (App.Current as App)?.Services?.GetService<NotificationService>();
    if (notifSvc is not null)
    {
      var manager = new WindowNotificationManager(this)
      {
        Position = NotificationPosition.BottomRight,
        MaxItems = 3
      };
      notifSvc.SetManager(manager);
    }
  }
}
