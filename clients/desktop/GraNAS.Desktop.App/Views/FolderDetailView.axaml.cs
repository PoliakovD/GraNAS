using Avalonia.ReactiveUI;
using GraNAS.Desktop.App.ViewModels;

namespace GraNAS.Desktop.App.Views;

public partial class FolderDetailView : ReactiveUserControl<FolderDetailViewModel>
{
  public FolderDetailView()
  {
    InitializeComponent();
  }
}
