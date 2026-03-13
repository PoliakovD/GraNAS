using System.ComponentModel.DataAnnotations;

namespace GraNAS.Models.DTO;

public class CreateFolderRequest
{
  [Required]
  [MaxLength(255)]
  public string Name { get; set; }

  public Guid? ParentId { get; set; }
}
