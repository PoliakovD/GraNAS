using System.ComponentModel.DataAnnotations;

namespace GraNAS.Auth.Models.DTO;

public class LoginRequest
{
  [Required]
  [EmailAddress(ErrorMessage = "Invalid email format.")]
  public string Email { get; set; } = null!;

  [Required]
  public string Password { get; set; } = null!;
}
