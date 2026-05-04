using Avalonia.ReactiveUI;
using GraNAS.Desktop.App.ViewModels;

namespace GraNAS.Desktop.App.Views;

public partial class LoginView : ReactiveUserControl<LoginViewModel>
{
  public LoginView()
  {
    InitializeComponent();
  }
}
