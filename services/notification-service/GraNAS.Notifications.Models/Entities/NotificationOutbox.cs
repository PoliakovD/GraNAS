namespace GraNAS.Notifications.Models.Entities;

public class NotificationOutbox
{
    public Guid Id { get; set; }
    public Guid NotificationEventId { get; set; }
    public DeliveryTarget Target { get; set; }
    public OutboxStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public NotificationEvent? NotificationEvent { get; set; }
}
