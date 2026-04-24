using System.ComponentModel.DataAnnotations;

namespace GraNAS.Sharing.Models.DTO;

public class CreateShareRequest
{
    [Required]
    public DateTime ExpiresAt { get; set; }

    [MaxLength(1024)]
    public string? Path { get; set; }
}
