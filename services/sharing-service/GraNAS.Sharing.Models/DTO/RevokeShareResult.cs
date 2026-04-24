namespace GraNAS.Sharing.Models.DTO;

public enum RevokeShareError { None, NotFoundOrForbidden }

public record RevokeShareResult(RevokeShareError Error);
