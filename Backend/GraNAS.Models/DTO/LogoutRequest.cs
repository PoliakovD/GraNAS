namespace GraNAS.Models.DTO;

public class LogoutRequest
{
  // Опционально: если нужно отозвать конкретный refresh token
  public string? RefreshToken { get; set; }

  // Флаг для завершения всех сессий пользователя
  public bool? AllSessions { get; set; }
}
