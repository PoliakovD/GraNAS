namespace GraNAS.Models;

public record User
{
  public Guid Id { get; set; }
  public string Email { get; set; }
  public string PasswordHash { get; set; }
  public bool IsAdmin { get; set; }
  public DateTime CreatedAt { get; set; }
}
