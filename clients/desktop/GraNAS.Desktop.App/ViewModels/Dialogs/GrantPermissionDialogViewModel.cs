using System.Reactive;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels.Dialogs;

public class GrantPermissionDialogViewModel : ReactiveObject
{
  private string _email = string.Empty;
  private AccessLevel _accessLevel = AccessLevel.View;

  public string Email
  {
    get => _email;
    set => this.RaiseAndSetIfChanged(ref _email, value);
  }

  public AccessLevel AccessLevel
  {
    get => _accessLevel;
    set => this.RaiseAndSetIfChanged(ref _accessLevel, value);
  }

  public IEnumerable<AccessLevel> Levels { get; } = Enum.GetValues<AccessLevel>();

  public ReactiveCommand<Unit, (string Email, AccessLevel Level)?> ConfirmCommand { get; }
  public ReactiveCommand<Unit, (string Email, AccessLevel Level)?> CancelCommand { get; }

  public GrantPermissionDialogViewModel()
  {
    var canConfirm = this.WhenAnyValue(x => x.Email, e => !string.IsNullOrWhiteSpace(e));
    ConfirmCommand = ReactiveCommand.Create<(string, AccessLevel)?>(() => (Email.Trim(), AccessLevel), canConfirm);
    CancelCommand = ReactiveCommand.Create<(string, AccessLevel)?>(() => null);
  }
}
