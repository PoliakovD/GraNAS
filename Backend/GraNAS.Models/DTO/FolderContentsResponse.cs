namespace GraNAS.Models.DTO;

public class FolderContentsResponse
{
  public IEnumerable<FolderResponse> Subfolders { get; set; }
  public IEnumerable<FileResponse> Files { get; set; }
}
