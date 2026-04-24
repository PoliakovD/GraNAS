namespace GraNAS.Sharing.Models.DTO;

public class ShareLinkResponse
{
    public Guid Id { get; set; }
    public Guid FolderId { get; set; }
    public string? Path { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public DateTime CreatedAt { get; set; }
}
