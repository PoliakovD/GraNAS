using System;

namespace GraNAS.Metadata.Models.DTO;

public class FolderResponse
{
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public int FilesCount { get; set; }
}
