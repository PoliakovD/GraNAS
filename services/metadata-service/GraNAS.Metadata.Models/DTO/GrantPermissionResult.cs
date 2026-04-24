namespace GraNAS.Metadata.Models.DTO;

public enum GrantPermissionError { None, UserNotFound, FolderNotFoundOrForbidden }

public class GrantPermissionResult
{
  public GrantPermissionError Error { get; }
  public PermissionResponse? Response { get; }

  private GrantPermissionResult(GrantPermissionError error, PermissionResponse? response = null)
  {
    Error = error;
    Response = response;
  }

  public static GrantPermissionResult Success(PermissionResponse response) => new(GrantPermissionError.None, response);
  public static GrantPermissionResult UserNotFound() => new(GrantPermissionError.UserNotFound);
  public static GrantPermissionResult FolderNotFoundOrForbidden() => new(GrantPermissionError.FolderNotFoundOrForbidden);
}
