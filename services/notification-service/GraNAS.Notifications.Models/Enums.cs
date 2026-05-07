namespace GraNAS.Notifications.Models;

public enum DeliveryTarget
{
    Email,
    SignalR
}

public enum OutboxStatus
{
    Pending,
    Delivered,
    Failed
}
