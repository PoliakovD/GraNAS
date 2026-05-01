using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.API.Hubs;

public class SignalingHub : Hub
{
    private readonly ISessionStore _sessions;
    private readonly IAccessChecker _access;
    private readonly IDeviceService _devices;
    private readonly ILogger<SignalingHub> _logger;

    public SignalingHub(ISessionStore sessions, IAccessChecker access, IDeviceService devices, ILogger<SignalingHub> logger)
    {
        _sessions = sessions;
        _access = access;
        _devices = devices;
        _logger = logger;
    }

    /// <summary>Client registers its device identity. Must be called before JoinAsOwner.</summary>
    public async Task RegisterDevice(Guid deviceId)
    {
        if (!IsAuthenticated())
            throw new HubException("Authentication required.");

        var userId = GetUserId();

        if (!await _devices.BelongsToUserAsync(deviceId, userId))
            throw new HubException("Unknown device. Call POST /api/signaling/devices first.");

        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _sessions.RegisterDeviceConnectionAsync(deviceId, Context.ConnectionId, userId, ip);
        Context.Items["DeviceId"] = deviceId;
        Context.Items["UserId"] = userId;

        _logger.LogInformation("Device {DeviceId} registered for user {UserId} (conn {ConnId}, ip {Ip})",
            deviceId, userId, Context.ConnectionId, ip);
    }

    /// <summary>Owner registers itself as active — marks the folder as «online».</summary>
    public async Task JoinAsOwner(Guid folderId)
    {
        if (!IsAuthenticated())
            throw new HubException("Authentication required to join as owner.");

        var deviceId = GetDeviceId()
            ?? throw new HubException("Call RegisterDevice before JoinAsOwner.");

        var userId = GetUserId();
        var access = await _access.CheckJwtAccessAsync(folderId, userId);

        if (access is null || access.OwnerId != userId)
        {
            _logger.LogWarning("Owner join refused: device {DeviceId} not authorized as owner of folder {FolderId} (userId={UserId})",
                deviceId, folderId, userId);
            throw new HubException("Not authorized as owner of this folder.");
        }

        await _sessions.RegisterOwnerAsync(folderId, deviceId);
        TrackOwnerFolder(folderId);

        await Groups.AddToGroupAsync(Context.ConnectionId, FolderGroupKey(folderId));
        await Clients.Group(FolderGroupKey(folderId))
            .SendAsync("OwnerOnlineStatusChanged", folderId, true);

        _logger.LogInformation("Owner device {DeviceId} joined for folder {FolderId} (conn {ConnId})",
            deviceId, folderId, Context.ConnectionId);
    }

    /// <summary>Owner explicitly goes offline for a folder.</summary>
    public async Task LeaveAsOwner(Guid folderId)
    {
        var deviceId = GetDeviceId();
        if (deviceId is null) return;

        var isLastOwner = await _sessions.RemoveOwnerAsync(folderId, deviceId.Value);
        RemoveOwnerFolder(folderId);

        if (isLastOwner)
        {
            await Clients.Group(FolderGroupKey(folderId))
                .SendAsync("OwnerOnlineStatusChanged", folderId, false);
        }
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, FolderGroupKey(folderId));
        _logger.LogInformation("Owner device {DeviceId} left folder {FolderId}", deviceId.Value, folderId);
    }

    /// <summary>Receiver subscribes to owner online status.</summary>
    public async Task WatchFolder(Guid folderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, FolderGroupKey(folderId));
        var ownerDeviceId = await _sessions.GetOwnerDeviceIdAsync(folderId);
        await Clients.Caller.SendAsync("OwnerOnlineStatusChanged", folderId, ownerDeviceId is not null);
        _logger.LogInformation("WatchFolder: conn {ConnId} watching folder {FolderId} (ownerOnline={OwnerOnline})",
            Context.ConnectionId, folderId, ownerDeviceId is not null);
    }

    /// <summary>Receiver initiates a P2P session. JWT or share token required.</summary>
    public async Task RequestSession(Guid folderId, string? shareToken = null)
    {
        FolderAccessResult? accessResult;

        if (IsAuthenticated())
        {
            accessResult = await _access.CheckJwtAccessAsync(folderId, GetUserId());
        }
        else if (!string.IsNullOrEmpty(shareToken))
        {
            accessResult = await _access.CheckShareTokenAsync(folderId, shareToken);
        }
        else
        {
            await Clients.Caller.SendAsync("AccessDenied", folderId, "Authentication or share token required.");
            return;
        }

        if (accessResult is null)
        {
            _logger.LogWarning("RequestSession denied: conn {ConnId} has no access to folder {FolderId}",
                Context.ConnectionId, folderId);
            await Clients.Caller.SendAsync("AccessDenied", folderId, "Access denied.");
            return;
        }

        var ownerDeviceId = await _sessions.GetOwnerDeviceIdAsync(folderId);
        if (ownerDeviceId is null)
        {
            _logger.LogWarning("RequestSession: owner of folder {FolderId} is offline (requester conn={ConnId})",
                folderId, Context.ConnectionId);
            await Clients.Caller.SendAsync("OwnerOffline", folderId);
            return;
        }

        var ownerConnId = await _sessions.GetConnectionIdByDeviceAsync(ownerDeviceId.Value);
        if (ownerConnId is null)
        {
            _logger.LogWarning("RequestSession: owner device {DeviceId} has no active connection (folder={FolderId})",
                ownerDeviceId.Value, folderId);
            await Clients.Caller.SendAsync("OwnerOffline", folderId);
            return;
        }

        await _sessions.RegisterSessionPairAsync(Context.ConnectionId, ownerConnId, folderId);
        await Clients.Client(ownerConnId).SendAsync(
            "IncomingPeerRequest", Context.ConnectionId, folderId, accessResult.ScopePath);

        _logger.LogInformation(
            "Session requested: receiver {ReceiverConnId} ↔ owner device {DeviceId} (conn {OwnerConnId}) for folder {FolderId}",
            Context.ConnectionId, ownerDeviceId.Value, ownerConnId, folderId);
    }

    /// <summary>Relay SDP offer from owner to receiver.</summary>
    public async Task SendOffer(string targetConnectionId, string sdp)
    {
        await AssertValidSessionAsync("SendOffer", targetConnectionId);
        await Clients.Client(targetConnectionId).SendAsync("Offer", Context.ConnectionId, sdp);
        _logger.LogDebug("SendOffer forwarded {From} → {To} (sdpLength={Length})",
            Context.ConnectionId, targetConnectionId, sdp.Length);
    }

    /// <summary>Relay SDP answer from receiver to owner.</summary>
    public async Task SendAnswer(string targetConnectionId, string sdp)
    {
        await AssertValidSessionAsync("SendAnswer", targetConnectionId);
        await Clients.Client(targetConnectionId).SendAsync("Answer", Context.ConnectionId, sdp);
        _logger.LogDebug("SendAnswer forwarded {From} → {To} (sdpLength={Length})",
            Context.ConnectionId, targetConnectionId, sdp.Length);
    }

    /// <summary>Relay ICE candidate between peers.</summary>
    public async Task SendIceCandidate(string targetConnectionId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        await AssertValidSessionAsync("SendIceCandidate", targetConnectionId);
        LogIceCandidateType(candidate);
        await Clients.Client(targetConnectionId)
            .SendAsync("IceCandidate", Context.ConnectionId, candidate, sdpMid, sdpMLineIndex);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = GetDeviceId();
        var userId = GetUserId();

        foreach (var folderId in GetOwnerFolders())
        {
            if (deviceId.HasValue)
            {
                var isLastOwner = await _sessions.RemoveOwnerAsync(folderId, deviceId.Value);
                if (isLastOwner)
                {
                    await Clients.Group(FolderGroupKey(folderId))
                        .SendAsync("OwnerOnlineStatusChanged", folderId, false);
                }
            }
        }

        if (deviceId.HasValue)
            await _sessions.RemoveDeviceConnectionAsync(deviceId.Value, Context.ConnectionId, userId);

        await _sessions.RemoveConnectionAsync(Context.ConnectionId);

        _logger.LogInformation("Connection {ConnId} disconnected (device {DeviceId})", Context.ConnectionId, deviceId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task AssertValidSessionAsync(string methodName, string targetConnectionId)
    {
        if (!await _sessions.IsValidSessionPairAsync(Context.ConnectionId, targetConnectionId))
        {
            _logger.LogWarning("{Method} rejected: invalid session pair {From} ↔ {To}",
                methodName, Context.ConnectionId, targetConnectionId);
            throw new HubException("Invalid or expired session.");
        }
    }

    private void LogIceCandidateType(string candidate)
    {
        var typ = "unknown";
        var typIdx = candidate.IndexOf(" typ ", StringComparison.Ordinal);
        if (typIdx >= 0)
        {
            var rest = candidate[(typIdx + 5)..];
            typ = rest.Split(' ')[0];
        }
        _logger.LogInformation("ICE candidate type={IceCandidateType} from {ConnId}", typ, Context.ConnectionId);
    }

    private bool IsAuthenticated() => Context.User?.Identity?.IsAuthenticated == true;

    private Guid GetUserId()
    {
        if (Context.Items.TryGetValue("UserId", out var cached) && cached is Guid cachedId)
            return cachedId;
        var sub = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private Guid? GetDeviceId()
        => Context.Items.TryGetValue("DeviceId", out var val) && val is Guid g ? g : null;

    private static string FolderGroupKey(Guid folderId) => $"folder:{folderId}";

    private void TrackOwnerFolder(Guid folderId)
    {
        if (!Context.Items.TryGetValue("OwnerFolders", out var existing))
            Context.Items["OwnerFolders"] = new HashSet<Guid> { folderId };
        else
            ((HashSet<Guid>)existing!).Add(folderId);
    }

    private void RemoveOwnerFolder(Guid folderId)
    {
        if (Context.Items.TryGetValue("OwnerFolders", out var existing))
            ((HashSet<Guid>)existing!).Remove(folderId);
    }

    private IEnumerable<Guid> GetOwnerFolders()
    {
        if (Context.Items.TryGetValue("OwnerFolders", out var existing))
            return new List<Guid>((HashSet<Guid>)existing!);
        return [];
    }

}
