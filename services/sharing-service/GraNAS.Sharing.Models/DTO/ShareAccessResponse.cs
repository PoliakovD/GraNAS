namespace GraNAS.Sharing.Models.DTO;

public class ShareAccessResponse
{
    public Guid FolderId { get; set; }
    public Guid OwnerId { get; set; }
    public string? ScopePath { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}
