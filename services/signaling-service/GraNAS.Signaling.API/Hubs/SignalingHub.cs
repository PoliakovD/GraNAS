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
    private readonly ILogger<SignalingHub> _logger;

    public SignalingHub(ISessionStore sessions, IAccessChecker access, ILogger<SignalingHub> logger)
    {
        _sessions = sessions;
        _access = access;
        _logger = logger;
    }

    /// <summary>Owner регистрирует себя как активного — помечает папку как «online».</summary>
    public async Task JoinAsOwner(Guid folderId)
    {
        if (!IsAuthenticated())
            throw new HubException("Authentication required to join as owner.");

        var userId = GetUserId();
        var access = await _access.CheckJwtAccessAsync(folderId, userId);

        if (access is null || access.OwnerId != userId)
            throw new HubException("Not authorized as owner of this folder.");

        await _sessions.RegisterOwnerAsync(folderId, Context.ConnectionId);
        TrackOwnerFolder(folderId);

        await Groups.AddToGroupAsync(Context.ConnectionId, FolderGroupKey(folderId));
        await Clients.Group(FolderGroupKey(folderId))
            .SendAsync("OwnerOnlineStatusChanged", folderId, true);

        _logger.LogInformation(
            "Owner {UserId} joined for folder {FolderId} (conn {ConnId})",
            userId, folderId, Context.ConnectionId);
    }

    /// <summary>Owner явно переходит в offline для папки (toggle).</summary>
    public async Task LeaveAsOwner(Guid folderId)
    {
        await _sessions.RemoveOwnerAsync(folderId, Context.ConnectionId);
        RemoveOwnerFolder(folderId);

        await Clients.Group(FolderGroupKey(folderId))
            .SendAsync("OwnerOnlineStatusChanged", folderId, false);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, FolderGroupKey(folderId));
    }

    /// <summary>Receiver подписывается на онлайн-статус owner-а (для индикатора).</summary>
    public async Task WatchFolder(Guid folderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, FolderGroupKey(folderId));
        var ownerConnId = await _sessions.GetOwnerConnectionIdAsync(folderId);
        await Clients.Caller.SendAsync("OwnerOnlineStatusChanged", folderId, ownerConnId is not null);
    }

    /// <summary>Receiver инициирует P2P-сессию. JWT или share-токен обязателен.</summary>
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
            await Clients.Caller.SendAsync("AccessDenied", folderId, "Access denied.");
            return;
        }

        var ownerConnId = await _sessions.GetOwnerConnectionIdAsync(folderId);
        if (ownerConnId is null)
        {
            await Clients.Caller.SendAsync("OwnerOffline", folderId);
            return;
        }

        await _sessions.RegisterSessionPairAsync(Context.ConnectionId, ownerConnId, folderId);
        await Clients.Client(ownerConnId).SendAsync(
            "IncomingPeerRequest", Context.ConnectionId, folderId, accessResult.ScopePath);

        _logger.LogInformation(
            "Session requested: receiver {ReceiverConnId} ↔ owner {OwnerConnId} for folder {FolderId}",
            Context.ConnectionId, ownerConnId, folderId);
    }

    /// <summary>Relay SDP offer от owner к receiver.</summary>
    public async Task SendOffer(string targetConnectionId, string sdp)
    {
        await AssertValidSessionAsync(targetConnectionId);
        await Clients.Client(targetConnectionId).SendAsync("Offer", Context.ConnectionId, sdp);
    }

    /// <summary>Relay SDP answer от receiver к owner.</summary>
    public async Task SendAnswer(string targetConnectionId, string sdp)
    {
        await AssertValidSessionAsync(targetConnectionId);
        await Clients.Client(targetConnectionId).SendAsync("Answer", Context.ConnectionId, sdp);
    }

    /// <summary>Relay ICE-кандидата между пирами.</summary>
    public async Task SendIceCandidate(string targetConnectionId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        await AssertValidSessionAsync(targetConnectionId);
        _logger.LogDebug(
            "ICE candidate relayed {From} → {To}: {Candidate}",
            Context.ConnectionId, targetConnectionId, candidate);
        await Clients.Client(targetConnectionId)
            .SendAsync("IceCandidate", Context.ConnectionId, candidate, sdpMid, sdpMLineIndex);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var folderId in GetOwnerFolders())
        {
            await _sessions.RemoveOwnerAsync(folderId, Context.ConnectionId);
            await Clients.Group(FolderGroupKey(folderId))
                .SendAsync("OwnerOnlineStatusChanged", folderId, false);
        }

        await _sessions.RemoveConnectionAsync(Context.ConnectionId);

        _logger.LogInformation("Connection {ConnId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task AssertValidSessionAsync(string targetConnectionId)
    {
        if (!await _sessions.IsValidSessionPairAsync(Context.ConnectionId, targetConnectionId))
            throw new HubException("Invalid or expired session.");
    }

    private bool IsAuthenticated() => Context.User?.Identity?.IsAuthenticated == true;

    private Guid GetUserId()
    {
        var sub = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

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
