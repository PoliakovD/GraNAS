using System;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Notifications.Models.Entities;
using GraNAS.Notifications.Models.Repositories;
using GraNAS.Notifications.Services.Options;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GraNAS.Notifications.API.Controllers;

[ApiController]
[Route("api/notifications")]
public class PushSubscriptionsController : ControllerBase
{
    private readonly IPushSubscriptionRepository _subs;
    private readonly IOptions<WebPushOptions> _vapid;
    private readonly ILogger<PushSubscriptionsController> _logger;

    public PushSubscriptionsController(
        IPushSubscriptionRepository subs,
        IOptions<WebPushOptions> vapid,
        ILogger<PushSubscriptionsController> logger)
    {
        _subs = subs;
        _vapid = vapid;
        _logger = logger;
    }

    /// <summary>VAPID public key — анонимный</summary>
    [HttpGet("push/vapid-public-key")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(VapidKeyResponse), StatusCodes.Status200OK)]
    public IActionResult GetVapidKey()
        => Ok(new VapidKeyResponse(_vapid.Value.PublicKey));

    /// <summary>Подписаться на web push</summary>
    [Authorize]
    [HttpPost("push-subscriptions")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest request, CancellationToken ct)
    {
        var userId = ParseUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new ErrorResponse { Error = "invalid_token", ErrorDescription = "Invalid user identifier." });

        var sub = new PushSubscription
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            Endpoint  = request.Endpoint,
            P256dh    = request.Keys.P256dh,
            Auth      = request.Keys.Auth,
            UserAgent = Request.Headers.UserAgent.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        await _subs.UpsertAsync(sub, ct);
        _logger.LogInformation("Push subscription created/updated for user {UserId}", userId);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>Отписаться от web push</summary>
    [Authorize]
    [HttpDelete("push-subscriptions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unsubscribe([FromQuery] string endpoint, CancellationToken ct)
    {
        var userId = ParseUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new ErrorResponse { Error = "invalid_token", ErrorDescription = "Invalid user identifier." });

        await _subs.DeleteByEndpointAsync(endpoint, userId, ct);
        return NoContent();
    }

    private Guid ParseUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public record VapidKeyResponse([property: JsonPropertyName("publicKey")] string PublicKey);

public class PushSubscribeRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Endpoint { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Required]
    public PushKeys Keys { get; set; } = null!;
}

public class PushKeys
{
    [System.ComponentModel.DataAnnotations.Required]
    [JsonPropertyName("p256dh")]
    public string P256dh { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Required]
    public string Auth { get; set; } = null!;
}
