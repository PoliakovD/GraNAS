using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class FolderDetailViewModel : ViewModelBase
{
  public FolderResponse Folder { get; }
  public PermissionsListViewModel Permissions { get; }
  public SharesListViewModel Shares { get; }

  public FolderDetailViewModel(
    FolderResponse folder,
    IPermissionsApi permissionsApi,
    ISharesApi sharesApi)
  {
    Folder = folder;
    Permissions = new PermissionsListViewModel(permissionsApi, folder.Id);
    Shares = new SharesListViewModel(sharesApi, folder.Id);
  }
}
