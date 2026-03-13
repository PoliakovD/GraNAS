namespace GraNAS.Models.DTO;

public class FileResponse
{
  public Guid Id { get; set; }
  public Guid FolderId { get; set; }
  public string Name { get; set; }
  public string Type { get; set; }
  public long Size { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
}
