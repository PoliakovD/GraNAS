using System.Reactive;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels.Dialogs;

public class CreateFolderDialogViewModel : ReactiveObject
{
  private string _name = string.Empty;

  public string Name
  {
    get => _name;
    set => this.RaiseAndSetIfChanged(ref _name, value);
  }

  public ReactiveCommand<Unit, string?> ConfirmCommand { get; }
  public ReactiveCommand<Unit, string?> CancelCommand { get; }

  public CreateFolderDialogViewModel()
  {
    var canConfirm = this.WhenAnyValue(x => x.Name, n => !string.IsNullOrWhiteSpace(n));
    ConfirmCommand = ReactiveCommand.Create<string?>(() => Name.Trim(), canConfirm);
    CancelCommand = ReactiveCommand.Create<string?>(() => null);
  }
}
