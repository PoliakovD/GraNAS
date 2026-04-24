using System.ComponentModel.DataAnnotations;

namespace GraNAS.Metadata.Models.DTO;

public class GrantPermissionRequest
{
  [Required]
  [EmailAddress]
  public string Email { get; set; } = null!;

  [Required]
  public AccessLevel AccessLevel { get; set; }

  [MaxLength(1024)]
  public string? Path { get; set; }
}
