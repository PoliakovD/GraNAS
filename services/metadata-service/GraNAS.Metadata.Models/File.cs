using System;

namespace GraNAS.Metadata.Models;

public class File
{
  public Guid Id { get; set; }
  public Guid FolderId { get; set; }
  public Guid OwnerId { get; set; }
  public string Name { get; set; } = null!;
  public string Type { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }

  public Folder Folder { get; set; } = null!;
}
