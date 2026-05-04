using Avalonia.Controls;
using Avalonia.ReactiveUI;
using GraNAS.Desktop.App.ViewModels.Dialogs;
using ReactiveUI;

namespace GraNAS.Desktop.App.Views.Dialogs;

public partial class ShareCreatedDialog : ReactiveWindow<ShareCreatedDialogViewModel>
{
  public ShareCreatedDialog()
  {
    InitializeComponent();
    this.WhenActivated(d =>
    {
      // Copy: copies token to clipboard then closes.
      // Atomic: clipboard operation completes before window is destroyed.
      ViewModel!.CopyCommand.Subscribe(_ => DoCopyAndClose(ViewModel!.Token));

      // Close: just closes without copying.
      ViewModel!.CloseCommand.Subscribe(_ => Close());
    });
  }

  private async void DoCopyAndClose(string text)
  {
    try
    {
      var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
      if (clipboard is not null)
        await clipboard.SetTextAsync(text);
    }
    catch
    {
      // Clipboard unavailable — still close.
    }
    finally
    {
      Close();
    }
  }
}
