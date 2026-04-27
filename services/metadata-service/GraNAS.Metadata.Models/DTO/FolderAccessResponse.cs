namespace GraNAS.Metadata.Models.DTO;

public class FolderAccessResponse
{
    public Guid FolderId { get; set; }
    public Guid OwnerId { get; set; }
    public string? ScopePath { get; set; }
}
