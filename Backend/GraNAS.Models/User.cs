using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Models;

[Table("table_users")]
[Index(nameof(Email), IsUnique = true)]
public class User
{
  [Key]
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public Guid Id { get; set; }

  [Required]
  [MaxLength(255)]
  [EmailAddress]
  public string Email { get; set; }

  [Required]
  [Column("password_hash")]
  public string PasswordHash { get; set; }

  [Column("is_admin")]
  public bool IsAdmin { get; set; }

  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  [Column("created_at")]
  public DateTime CreatedAt { get; set; }

  public ICollection<RefreshToken> RefreshTokens { get; set; }
}
