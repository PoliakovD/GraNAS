using System.ComponentModel.DataAnnotations;

namespace GraNAS.Auth.Models.DTO;

public class RefreshRequest
{
  [Required]
  public string RefreshToken { get; set; } = null!;
}
