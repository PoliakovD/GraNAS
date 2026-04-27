using Avalonia.ReactiveUI;
using GraNAS.Desktop.App.ViewModels;

namespace GraNAS.Desktop.App.Views;

public partial class MyFoldersView : ReactiveUserControl<MyFoldersViewModel>
{
  public MyFoldersView()
  {
    InitializeComponent();
  }
}
