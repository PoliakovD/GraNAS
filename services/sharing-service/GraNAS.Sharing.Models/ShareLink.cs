namespace GraNAS.Sharing.Models;

public class ShareLink
{
    public Guid Id { get; set; }
    public Guid FolderId { get; set; }
    public Guid OwnerId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? Path { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
