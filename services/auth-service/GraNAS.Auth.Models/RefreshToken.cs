using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;


namespace GraNAS.Models;

[Table("table_refresh_tokens")]
[Index(nameof(Token), IsUnique = true)]
public class RefreshToken
{
  [Key]
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public Guid Id { get; set; }

  [Required]
  [ForeignKey(nameof(User))]
  [Column("user_id")]
  public Guid UserId { get; set; }
  [Column("token")]
  [Required] [MaxLength(255)] public string Token { get; set; }

  [Required] [Column("expires")] public DateTime Expires { get; set; }

  [Column("revoked")] public DateTime? Revoked { get; set; }

  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  [Column("created_at")]
  public DateTime CreatedAt { get; set; }
  
  public User User { get; set; }
}
