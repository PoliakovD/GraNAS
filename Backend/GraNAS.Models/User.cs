using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GraNAS.Models;

[Table("table_users")]
public record User
{
  [Column("id")]
  [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public Guid Id { get; set; }

  [Column("email")] [Required] public string Email { get; set; }
  [Column("password")] [Required] public string PasswordHash { get; set; }
  [Column("is_admin")] public bool IsAdmin { get; set; }
  [Column("created_at")] public DateTime CreatedAt { get; set; }
}
