using Avalonia.ReactiveUI;
using GraNAS.Desktop.App.ViewModels.Dialogs;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.Views.Dialogs;

public partial class GrantPermissionDialog : ReactiveWindow<GrantPermissionDialogViewModel>
{
  public GrantPermissionDialog()
  {
    InitializeComponent();
    this.WhenActivated(d =>
    {
      ViewModel!.ConfirmCommand.Subscribe(result => Close(result));
      ViewModel!.CancelCommand.Subscribe(_ => Close(null));
    });
  }
}
