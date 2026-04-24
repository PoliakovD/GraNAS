using System;

namespace GraNAS.Auth.Models.DTO;

public class UserLookupResponse
{
  public Guid Id { get; set; }
  public string Email { get; set; } = null!;
}
