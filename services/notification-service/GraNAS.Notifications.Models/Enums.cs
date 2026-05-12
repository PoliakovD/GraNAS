namespace GraNAS.Notifications.Models;

public enum DeliveryTarget
{
    Email,
    SignalR,
    WebPush
}

public enum OutboxStatus
{
    Pending,
    Delivered,
    Failed
}
