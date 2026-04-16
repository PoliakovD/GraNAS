using System.ComponentModel.DataAnnotations;
namespace GraNAS.Models.DTO;

public class LoginRequest
{
  [Required]
  [EmailAddress(ErrorMessage = "Invalid email format.")]
  public string Email { get; set; }

  [Required]
  public string Password { get; set; }
}
