using System.Reactive;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels.Dialogs;

public class CreateShareDialogViewModel : ReactiveObject
{
  private DateTime _expiresAt = DateTime.Today.AddDays(7);

  public DateTime ExpiresAt
  {
    get => _expiresAt;
    set => this.RaiseAndSetIfChanged(ref _expiresAt, value);
  }

  // Min = tomorrow
  public DateTime MinDate { get; } = DateTime.Today.AddDays(1);

  public ReactiveCommand<Unit, DateTime?> ConfirmCommand { get; }
  public ReactiveCommand<Unit, DateTime?> CancelCommand { get; }

  public CreateShareDialogViewModel()
  {
    ConfirmCommand = ReactiveCommand.Create<DateTime?>(() => ExpiresAt);
    CancelCommand = ReactiveCommand.Create<DateTime?>(() => null);
  }
}
