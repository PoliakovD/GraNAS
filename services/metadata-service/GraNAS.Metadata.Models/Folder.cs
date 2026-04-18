using System;
using System.Collections.Generic;

namespace GraNAS.Metadata.Models;

public class Folder
{
  public Guid Id { get; set; }
  public Guid OwnerId { get; set; }
  public string Name { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }

  public ICollection<File> Files { get; set; } = new List<File>();
}
