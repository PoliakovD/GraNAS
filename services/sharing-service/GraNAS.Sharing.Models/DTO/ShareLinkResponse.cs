namespace GraNAS.Sharing.Models.DTO;

public class ShareLinkResponse
{
    public Guid Id { get; set; }
    public Guid FolderId { get; set; }
    public string? Path { get; set; }
    public string ShareUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public DateTime CreatedAt { get; set; }
}
