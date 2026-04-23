namespace GraNAS.Metadata.Models.DTO;

public enum CreateFolderError { None, ParentNotFoundOrForbidden }

public class CreateFolderResult
{
  public CreateFolderError Error { get; }
  public FolderResponse? Response { get; }

  private CreateFolderResult(CreateFolderError error, FolderResponse? response = null)
  {
    Error = error;
    Response = response;
  }

  public static CreateFolderResult Success(FolderResponse response) => new(CreateFolderError.None, response);
  public static CreateFolderResult ParentNotFoundOrForbidden() => new(CreateFolderError.ParentNotFoundOrForbidden);
}
