using System;

namespace GraNAS.Auth.Models.DTO;

public class RegisterResponse
{
  public Guid UserId { get; set; }
  public string Message { get; set; } = null!;
}
