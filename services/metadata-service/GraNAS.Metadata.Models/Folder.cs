using System;

namespace GraNAS.Metadata.Models;

public class Folder
{
  public Guid Id { get; set; }
  public Guid OwnerId { get; set; }
  public string Name { get; set; } = null!;
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
}
