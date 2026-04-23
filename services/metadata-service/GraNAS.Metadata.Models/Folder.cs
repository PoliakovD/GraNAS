using System;
using System.Collections.Generic;

namespace GraNAS.Metadata.Models;

public class Folder
{
  public Guid Id { get; set; }
  public Guid OwnerId { get; set; }
  public Guid? ParentFolderId { get; set; }
  public string Name { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }

  // Навигационные свойства для self-referencing relationship
  public Folder? ParentFolder { get; set; }
  public ICollection<Folder> ChildFolders { get; set; } = new List<Folder>();
}
