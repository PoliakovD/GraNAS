namespace GraNAS.Signaling.Models.DTO;

/// <summary>
/// Эфемерные учётные данные для TURN-сервера (RFC 8489).
/// Возвращаются эндпоинтом <c>GET /api/turn/credentials</c> и используются клиентом
/// при создании <c>RTCPeerConnection</c> в качестве ICE-сервера.
/// </summary>
/// <param name="Username">Имя пользователя в формате <c>"{unixExpiry}:{userId}"</c>.</param>
/// <param name="Credential">Base64-строка HMAC-SHA1(secret, Username). Secret разделяется с coturn.</param>
/// <param name="Uris">Список URI TURN-сервера (например, <c>turn:host:3478?transport=udp</c>).</param>
/// <param name="Ttl">Время жизни учётных данных в секундах (обычно 600).</param>
public record TurnCredentialsResponse(
    string Username,
    string Credential,
    string[] Uris,
    int Ttl);
