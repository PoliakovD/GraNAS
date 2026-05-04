namespace GraNAS.Signaling.Models.DTO;

public class SessionInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public Guid UserId { get; set; }
}
