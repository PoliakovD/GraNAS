using System.ComponentModel.DataAnnotations;
namespace GraNAS.Models.DTO;

public class RefreshRequest
{
  [Required]
  public string RefreshToken { get; set; }
}
