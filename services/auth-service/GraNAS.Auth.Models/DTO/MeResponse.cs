using System;

namespace GraNAS.Auth.Models.DTO;

public class MeResponse
{
  public Guid Id { get; set; }
  public string Email { get; set; } = string.Empty;
  public bool IsAdmin { get; set; }
}
