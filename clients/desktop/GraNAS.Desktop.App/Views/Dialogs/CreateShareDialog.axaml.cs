using Avalonia.ReactiveUI;
using GraNAS.Desktop.App.ViewModels.Dialogs;
using ReactiveUI;

namespace GraNAS.Desktop.App.Views.Dialogs;

public partial class CreateShareDialog : ReactiveWindow<CreateShareDialogViewModel>
{
  public CreateShareDialog()
  {
    InitializeComponent();
    this.WhenActivated(d =>
    {
      ViewModel!.ConfirmCommand.Subscribe(result => Close(result));
      ViewModel!.CancelCommand.Subscribe(_ => Close(null));
    });
  }
}
