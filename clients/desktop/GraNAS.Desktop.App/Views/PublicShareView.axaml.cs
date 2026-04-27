using Avalonia.ReactiveUI;
using GraNAS.Desktop.App.ViewModels;

namespace GraNAS.Desktop.App.Views;

public partial class PublicShareView : ReactiveUserControl<PublicShareViewModel>
{
  public PublicShareView()
  {
    InitializeComponent();
  }
}
