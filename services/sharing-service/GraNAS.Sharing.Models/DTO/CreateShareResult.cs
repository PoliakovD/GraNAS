namespace GraNAS.Sharing.Models.DTO;

public enum CreateShareError { None, FolderNotFoundOrForbidden }

public class CreateShareResult
{
    public CreateShareError Error { get; private init; }
    public CreateShareResponse? Response { get; private init; }

    public static CreateShareResult Success(CreateShareResponse response) =>
        new() { Error = CreateShareError.None, Response = response };

    public static CreateShareResult FolderNotFoundOrForbidden() =>
        new() { Error = CreateShareError.FolderNotFoundOrForbidden };
}
