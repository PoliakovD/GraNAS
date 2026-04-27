using Avalonia.ReactiveUI;
using GraNAS.Desktop.App.ViewModels;

namespace GraNAS.Desktop.App.Views;

public partial class RegisterView : ReactiveUserControl<RegisterViewModel>
{
  public RegisterView()
  {
    InitializeComponent();
  }
}
