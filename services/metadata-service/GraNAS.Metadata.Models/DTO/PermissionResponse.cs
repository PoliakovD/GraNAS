using System;

namespace GraNAS.Metadata.Models.DTO;

public class PermissionResponse
{
  public Guid UserId { get; set; }
  public string? Email { get; set; }
  public AccessLevel AccessLevel { get; set; }
  public string? Path { get; set; }
  public DateTime CreatedAt { get; set; }
}
