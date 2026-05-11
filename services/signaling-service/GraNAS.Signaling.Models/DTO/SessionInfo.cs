namespace GraNAS.Signaling.Models.DTO;

/// <summary>
/// Сведения об активной сессии устройства в SignalR-хабе.
/// Сериализуется в JSON и сохраняется в Redis под ключом <c>signaling:session:{deviceId}</c>.
/// </summary>
public class SessionInfo
{
    /// <summary>SignalR <c>connectionId</c> текущего подключения. Меняется при каждом переподключении.</summary>
    public string ConnectionId { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public Guid UserId { get; set; }
}
