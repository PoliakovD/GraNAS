using System.Reactive;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels.Dialogs;

public class ShareCreatedDialogViewModel : ReactiveObject
{
  public string Token { get; }

  // Signals copy intent — actual clipboard access is done in code-behind
  // because clipboard requires a TopLevel reference (the dialog window itself).
  public ReactiveCommand<Unit, Unit> CopyCommand { get; }
  public ReactiveCommand<Unit, Unit> CloseCommand { get; }

  public ShareCreatedDialogViewModel(string token)
  {
    Token = token;
    CopyCommand = ReactiveCommand.Create(() => { });
    CloseCommand = ReactiveCommand.Create(() => { });
  }
}
