using System;
using System.Collections.Generic;

namespace GraNAS.Auth.Models;

public class User
{
  public Guid Id { get; set; }
  public string Email { get; set; } = null!;
  public string PasswordHash { get; set; } = null!;
  public bool IsAdmin { get; set; }
  public DateTime CreatedAt { get; set; }

  public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
