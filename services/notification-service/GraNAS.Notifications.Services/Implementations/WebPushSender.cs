using GraNAS.Notifications.Models.Entities;
using GraNAS.Notifications.Services.Interfaces;
using GraNAS.Notifications.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPushLib = WebPush;

namespace GraNAS.Notifications.Services.Implementations;

public class WebPushSender : IWebPushSender
{
    private readonly WebPushOptions _opts;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(IOptions<WebPushOptions> opts, ILogger<WebPushSender> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(PushSubscription sub, string payloadJson, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_opts.PublicKey) || string.IsNullOrEmpty(_opts.PrivateKey))
        {
            _logger.LogWarning("WebPushSender: VAPID keys not configured — skipping push");
            return true;
        }

        var subscription = new WebPushLib.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
        var vapid = new WebPushLib.VapidDetails(_opts.Subject, _opts.PublicKey, _opts.PrivateKey);

        using var client = new WebPushLib.WebPushClient();
        try
        {
            await client.SendNotificationAsync(subscription, payloadJson, vapid);
            _logger.LogDebug("WebPush sent to user {UserId} endpoint={Endpoint}", sub.UserId, sub.Endpoint[..Math.Min(40, sub.Endpoint.Length)]);
            return true;
        }
        catch (WebPushLib.WebPushException ex) when ((int)ex.StatusCode == 410 || (int)ex.StatusCode == 404)
        {
            _logger.LogInformation("WebPush subscription expired (410/404) for user {UserId} — removing", sub.UserId);
            return false;
        }
        catch (WebPushLib.WebPushException ex)
        {
            _logger.LogWarning(ex, "WebPush transient error for user {UserId}", sub.UserId);
            throw;
        }
    }
}
