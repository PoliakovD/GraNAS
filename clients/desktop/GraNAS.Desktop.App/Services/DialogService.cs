using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
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

  public async Task<string?> ShowFolderPickerAsync(string title = "Выберите папку")
  {
    if (Owner is null) return null;
    var results = await Owner.StorageProvider.OpenFolderPickerAsync(
      new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
    return results.Count > 0 ? results[0].TryGetLocalPath() : null;
  }

  public async Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Подтвердить", string cancelText = "Отмена")
  {
    if (Owner is null) return false;

    var tcs = new TaskCompletionSource<bool>();
    var dlg = new Window
    {
      Title = title,
      Width = 380,
      Height = 148,
      CanResize = false,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };

    var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
    panel.Children.Add(new TextBlock
    {
      Text = message,
      TextWrapping = Avalonia.Media.TextWrapping.Wrap,
    });

    var buttons = new StackPanel
    {
      Orientation = Avalonia.Layout.Orientation.Horizontal,
      Spacing = 8,
      HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
    };
    var btnCancel = new Button { Content = cancelText };
    var btnConfirm = new Button { Content = confirmText };
    btnCancel.Click += (_, _) => { tcs.TrySetResult(false); dlg.Close(); };
    btnConfirm.Click += (_, _) => { tcs.TrySetResult(true); dlg.Close(); };
    buttons.Children.Add(btnCancel);
    buttons.Children.Add(btnConfirm);
    panel.Children.Add(buttons);
    dlg.Content = panel;

    await dlg.ShowDialog(Owner);
    return await tcs.Task;
  }
}
