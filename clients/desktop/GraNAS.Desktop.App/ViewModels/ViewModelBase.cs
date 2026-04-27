using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public abstract class ViewModelBase : ReactiveObject, IActivatableViewModel
{
  public ViewModelActivator Activator { get; } = new();
}
