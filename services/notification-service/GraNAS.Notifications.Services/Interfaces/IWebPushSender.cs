using GraNAS.Notifications.Models.Entities;

namespace GraNAS.Notifications.Services.Interfaces;

public interface IWebPushSender
{
    /// <summary>Returns false if subscription is expired (410 Gone) and should be removed.</summary>
    Task<bool> SendAsync(PushSubscription sub, string payloadJson, CancellationToken ct = default);
}
