namespace GraNAS.Signaling.Services.Interfaces;

/// <summary>
/// Эфемерные учётные данные для TURN-сервера, сгенерированные сервисом.
/// </summary>
/// <param name="Username">Имя пользователя в формате <c>"{unixExpiry}:{userId}"</c>.</param>
/// <param name="Credential">HMAC-SHA1(secret, Username), закодированный в Base64.</param>
/// <param name="Uris">Адреса TURN-серверов.</param>
/// <param name="Ttl">Время жизни в секундах.</param>
public record TurnCredentials(string Username, string Credential, string[] Uris, int Ttl);

/// <summary>Генератор краткосрочных TURN-учётных данных по алгоритму RFC 8489.</summary>
public interface ITurnCredentialService
{
    /// <summary>
    /// Генерирует эфемерные TURN-учётные данные для указанного пользователя.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя, включаемый в имя пользователя TURN.</param>
    /// <returns>Готовые учётные данные для передачи клиенту в конфигурацию <c>RTCPeerConnection</c>.</returns>
    TurnCredentials Generate(string userId);
}
