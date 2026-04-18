using System;

namespace GraNAS.Auth.Models;

public class RefreshToken
{
  public Guid Id { get; set; }
  public Guid UserId { get; set; }
  public string Token { get; set; } = null!;
  public DateTime Expires { get; set; }
  public DateTime? Revoked { get; set; }
  public DateTime CreatedAt { get; set; }

  public User User { get; set; } = null!;
}
