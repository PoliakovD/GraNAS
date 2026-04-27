using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using GraNAS.Desktop.App.ViewModels.Dialogs;
using GraNAS.Desktop.App.Views.Dialogs;
using GraNAS.Desktop.Contracts.Metadata;

namespace GraNAS.Desktop.App.Services;

public class DialogService : IDialogService
{
  private Window? Owner =>
    (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

  public async Task<string?> ShowCreateFolderAsync()
  {
    var vm = new CreateFolderDialogViewModel();
    var dlg = new CreateFolderDialog { DataContext = vm };
    return await dlg.ShowDialog<string?>(Owner!);
  }

  public async Task<(string Email, AccessLevel Level)?> ShowGrantPermissionAsync()
  {
    var vm = new GrantPermissionDialogViewModel();
    var dlg = new GrantPermissionDialog { DataContext = vm };
    return await dlg.ShowDialog<(string, AccessLevel)?>(Owner!);
  }

  public async Task<DateTime?> ShowCreateShareAsync()
  {
    var vm = new CreateShareDialogViewModel();
    var dlg = new CreateShareDialog { DataContext = vm };
    return await dlg.ShowDialog<DateTime?>(Owner!);
  }

  public async Task ShowShareCreatedAsync(string token)
  {
    var vm = new ShareCreatedDialogViewModel(token);
    var dlg = new ShareCreatedDialog { DataContext = vm };
    await dlg.ShowDialog(Owner!);
  }
}
