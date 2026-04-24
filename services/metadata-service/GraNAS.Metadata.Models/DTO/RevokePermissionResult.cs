namespace GraNAS.Metadata.Models.DTO;

public enum RevokePermissionError { None, FolderNotFoundOrForbidden, PermissionNotFound }

public record RevokePermissionResult(RevokePermissionError Error);
