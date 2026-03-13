using System.ComponentModel.DataAnnotations;
namespace GraNAS.Models.DTO;

public class CreateFileRequest
{
  [Required]
  [MaxLength(255)]
  public string Name { get; set; }

  [Required]
  [MaxLength(100)]
  public string Type { get; set; }

  [Required]
  public long Size { get; set; }

  [Required]
  public Guid FolderId { get; set; }
}
