using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Models;

[Table("table_folders")]
[Index(nameof(OwnerId), Name = "IX_folders_owner_id")]
[Index(nameof(ParentId), Name = "IX_folders_parent_id")]
public class Folder
{
  [Key]
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public Guid Id { get; set; }

  [Required]
  [Column("owner_id")]
  public Guid OwnerId { get; set; }

  [Column("parent_id")]
  public Guid? ParentId { get; set; }

  [Required]
  [MaxLength(255)]
  [Column("name")]
  public string Name { get; set; }

  [Column("created_at")]
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public DateTime CreatedAt { get; set; }

  [Column("updated_at")]
  public DateTime? UpdatedAt { get; set; }

  // Навигационные свойства
  [ForeignKey(nameof(OwnerId))]
  public User Owner { get; set; }
  [ForeignKey(nameof(ParentId))]
  public Folder Parent { get; set; }

  public ICollection<Folder> Subfolders { get; set; }
  public ICollection<File> Files { get; set; }
}
