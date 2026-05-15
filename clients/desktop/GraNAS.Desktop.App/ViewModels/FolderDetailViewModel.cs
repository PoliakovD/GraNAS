using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class FolderDetailViewModel : ViewModelBase
{
  public FolderResponse Folder { get; }
  public PermissionsListViewModel Permissions { get; }
  public SharesListViewModel Shares { get; }
  public FolderPropertiesViewModel Properties { get; }

  public FolderDetailViewModel(
    FolderResponse folder,
    IPermissionsApi permissionsApi,
    ISharesApi sharesApi,
    ISignalingApi signalingApi,
    IAuthSession session,
    IDialogService dialogs,
    INotificationService notifications)
  {
    Folder = folder;
    Permissions = new PermissionsListViewModel(permissionsApi, folder.Id, dialogs, notifications);
    Shares = new SharesListViewModel(sharesApi, folder.Id, dialogs, notifications);
    Properties = new FolderPropertiesViewModel(folder, signalingApi, session, notifications);
  }
}
