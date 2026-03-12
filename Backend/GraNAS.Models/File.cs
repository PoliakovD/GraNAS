using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Models;

[Table("table_files")]
[Index(nameof(FolderId), Name = "IX_files_folder_id")]
[Index(nameof(OwnerId), Name = "IX_files_owner_id")]
public class File
{
  [Key]
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public Guid Id { get; set; }

  [Required]
  [Column("folder_id")]
  public Guid FolderId { get; set; }

  [Required]
  [Column("owner_id")]
  public Guid OwnerId { get; set; }

  [Required]
  [MaxLength(255)]
  [Column("name")]
  public string Name { get; set; }

  [Required]
  [MaxLength(100)]
  [Column("type")]
  public string Type { get; set; }

  [Column("created_at")]
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public DateTime CreatedAt { get; set; }

  [Column("updated_at")]
  public DateTime? UpdatedAt { get; set; }

  // Навигационные свойства
  [ForeignKey(nameof(FolderId))]
  public Folder Folder { get; set; }

  [ForeignKey(nameof(OwnerId))]
  public User Owner { get; set; }
}
