using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.API.Hubs;

/// <summary>
/// Главный SignalR-хаб для P2P-сигналинга GraNAS. Маршрут: <c>/hubs/signaling</c>.
/// Отвечает за координацию WebRTC-соединений между owner'ом (desktop) и receiver'ом (браузер):
/// регистрацию устройств, управление онлайн-статусом папок, relay SDP/ICE кандидатов.
/// </summary>
/// <remarks>
/// Обязательная последовательность вызовов для owner'а:
/// <c>RegisterDevice(deviceId)</c> → <c>JoinAsOwner(folderId)</c>.
/// Без <c>RegisterDevice</c> попытка <c>JoinAsOwner</c> выбросит <see cref="HubException"/>.
///
/// JWT передаётся через query-параметр <c>?access_token=</c> (не Authorization header),
/// что необходимо для WebSocket-подключений через SignalR.
///
/// Серверные события, отправляемые клиентам:
/// <list type="bullet">
/// <item><c>OwnerOnlineStatusChanged(folderId, isOnline)</c> — изменился онлайн-статус owner'а папки</item>
/// <item><c>IncomingPeerRequest(receiverConnId, folderId, scopePath?)</c> — receiver хочет подключиться</item>
/// <item><c>Offer(senderConnId, sdp)</c> — relay SDP-offer от owner'а</item>
/// <item><c>Answer(senderConnId, sdp)</c> — relay SDP-answer от receiver'а</item>
/// <item><c>IceCandidate(senderConnId, candidate, sdpMid, sdpMLineIndex)</c> — relay ICE-кандидата</item>
/// <item><c>OwnerOffline(folderId)</c> — owner папки недоступен при RequestSession</item>
/// <item><c>AccessDenied(folderId, reason)</c> — отказ в доступе</item>
/// <item><c>ForceDisconnect</c> — принудительное отключение (через REST <c>DELETE /api/sessions/{deviceId}</c>)</item>
/// </list>
/// </remarks>
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

    /// <summary>
    /// Регистрирует устройство в текущей SignalR-сессии.
    /// Сохраняет маппинг <c>deviceId ↔ connectionId</c> в Redis и кеширует
    /// <c>DeviceId</c> и <c>UserId</c> в <c>Context.Items</c>.
    /// Должен вызываться первым перед любым другим методом.
    /// </summary>
    /// <param name="deviceId">Идентификатор устройства, ранее зарегистрированного через <c>POST /api/devices</c>.</param>
    /// <exception cref="HubException">Если пользователь не аутентифицирован или устройство не принадлежит ему.</exception>
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

    /// <summary>
    /// Owner регистрируется как активный для папки: добавляет устройство в множество онлайн-owner'ов
    /// и рассылает событие <c>OwnerOnlineStatusChanged(folderId, true)</c> всем наблюдателям папки.
    /// </summary>
    /// <param name="folderId">Папка, для которой owner объявляет себя доступным.</param>
    /// <exception cref="HubException">Если не вызван <c>RegisterDevice</c> или пользователь не является владельцем папки.</exception>
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

    /// <summary>
    /// Owner явно покидает папку: удаляет устройство из множества онлайн-owner'ов.
    /// Если это был последний owner — рассылает <c>OwnerOnlineStatusChanged(folderId, false)</c>.
    /// </summary>
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

    /// <summary>
    /// Подписывает receiver на изменения онлайн-статуса owner'а папки.
    /// Добавляет соединение в SignalR-группу <c>folder:{folderId}</c> и немедленно отправляет
    /// текущий статус через <c>OwnerOnlineStatusChanged</c>.
    /// </summary>
    public async Task WatchFolder(Guid folderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, FolderGroupKey(folderId));
        var ownerDeviceId = await _sessions.GetOwnerDeviceIdAsync(folderId);
        await Clients.Caller.SendAsync("OwnerOnlineStatusChanged", folderId, ownerDeviceId is not null);
        _logger.LogInformation("WatchFolder: conn {ConnId} watching folder {FolderId} (ownerOnline={OwnerOnline})",
            Context.ConnectionId, folderId, ownerDeviceId is not null);
    }

    /// <summary>
    /// Receiver инициирует P2P-сессию с owner'ом папки.
    /// Проверяет права доступа (JWT или share token), находит онлайн-owner'а,
    /// регистрирует сессионную пару в Redis и отправляет owner'у событие <c>IncomingPeerRequest</c>.
    /// </summary>
    /// <param name="folderId">Папка, к которой запрашивается P2P-доступ.</param>
    /// <param name="shareToken">
    /// Сырой base64url share-токен для анонимного доступа. <c>null</c> — если пользователь аутентифицирован через JWT.
    /// </param>
    /// <remarks>
    /// При отказе в доступе отправляет <c>AccessDenied</c> вместо исключения.
    /// При офлайн owner'е отправляет <c>OwnerOffline</c>.
    /// </remarks>
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

        // Phase 6.5+: explicit binding в table_device_folders имеет приоритет над Redis JoinAsOwner.
        // Защищает от сценария, когда несколько устройств вызвали JoinAsOwner для одной папки.
        var boundDeviceId = await _devices.GetBoundDeviceIdAsync(folderId);
        if (boundDeviceId is not null && boundDeviceId.Value != ownerDeviceId.Value)
        {
            var boundConnId = await _sessions.GetConnectionIdByDeviceAsync(boundDeviceId.Value);
            if (boundConnId is null)
            {
                _logger.LogWarning(
                    "RequestSession: folder {FolderId} bound to {BoundDeviceId} but it's offline; JoinAsOwner was on device {OnlineDeviceId}",
                    folderId, boundDeviceId.Value, ownerDeviceId.Value);
                await Clients.Caller.SendAsync("OwnerOffline", folderId);
                return;
            }
            _logger.LogInformation(
                "RequestSession: redirecting to bound device {BoundDeviceId} (JoinAsOwner was on {OnlineDeviceId}) for folder {FolderId}",
                boundDeviceId.Value, ownerDeviceId.Value, folderId);
            ownerDeviceId = boundDeviceId.Value;
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

    /// <summary>
    /// Передаёт SDP-offer от owner'а к receiver'у.
    /// Owner генерирует offer после получения события <c>IncomingPeerRequest</c>.
    /// </summary>
    /// <param name="targetConnectionId">SignalR connectionId receiver'а.</param>
    /// <param name="sdp">Строка SDP-offer.</param>
    /// <exception cref="HubException">Если <paramref name="targetConnectionId"/> не является зарегистрированным партнёром этого соединения.</exception>
    public async Task SendOffer(string targetConnectionId, string sdp)
    {
        await AssertValidSessionAsync("SendOffer", targetConnectionId);
        await Clients.Client(targetConnectionId).SendAsync("Offer", Context.ConnectionId, sdp);
        _logger.LogDebug("SendOffer forwarded {From} → {To} (sdpLength={Length})",
            Context.ConnectionId, targetConnectionId, sdp.Length);
    }

    /// <summary>
    /// Передаёт SDP-answer от receiver'а к owner'у.
    /// Receiver вызывает этот метод после получения события <c>Offer</c>.
    /// </summary>
    /// <param name="targetConnectionId">SignalR connectionId owner'а.</param>
    /// <param name="sdp">Строка SDP-answer.</param>
    /// <exception cref="HubException">Если <paramref name="targetConnectionId"/> не является зарегистрированным партнёром этого соединения.</exception>
    public async Task SendAnswer(string targetConnectionId, string sdp)
    {
        await AssertValidSessionAsync("SendAnswer", targetConnectionId);
        await Clients.Client(targetConnectionId).SendAsync("Answer", Context.ConnectionId, sdp);
        _logger.LogDebug("SendAnswer forwarded {From} → {To} (sdpLength={Length})",
            Context.ConnectionId, targetConnectionId, sdp.Length);
    }

    /// <summary>
    /// Передаёт ICE-кандидата между участниками P2P-сессии (в обоих направлениях).
    /// </summary>
    /// <param name="targetConnectionId">SignalR connectionId получателя.</param>
    /// <param name="candidate">Строка ICE-кандидата.</param>
    /// <param name="sdpMid">Идентификатор медиа-потока из SDP.</param>
    /// <param name="sdpMLineIndex">Индекс строки SDP.</param>
    /// <exception cref="HubException">Если <paramref name="targetConnectionId"/> не является зарегистрированным партнёром этого соединения.</exception>
    public async Task SendIceCandidate(string targetConnectionId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        await AssertValidSessionAsync("SendIceCandidate", targetConnectionId);
        LogIceCandidateType(candidate);
        await Clients.Client(targetConnectionId)
            .SendAsync("IceCandidate", Context.ConnectionId, candidate, sdpMid, sdpMLineIndex);
    }

    /// <summary>
    /// Owner явно отказывает receiver-у в P2P-сессии (например, папка привязана к другому устройству).
    /// Сервер проверяет наличие зарегистрированной сессионной пары и форвардит
    /// <c>AccessDenied(folderId, reason)</c> receiver-у.
    /// </summary>
    public async Task DenyPeerRequest(string receiverConnectionId, Guid folderId, string reason)
    {
        await AssertValidSessionAsync("DenyPeerRequest", receiverConnectionId);
        await Clients.Client(receiverConnectionId).SendAsync("AccessDenied", folderId, reason);
        _logger.LogInformation(
            "Owner {OwnerConnId} denied peer request from {ReceiverConnId} for folder {FolderId}: {Reason}",
            Context.ConnectionId, receiverConnectionId, folderId, reason);
    }

    /// <summary>
    /// Вызывается при отключении клиента (штатном или аварийном).
    /// Удаляет устройство из всех owned-папок и очищает Redis-состояние.
    /// Если устройство было последним owner'ом папки — рассылает <c>OwnerOnlineStatusChanged(folderId, false)</c>.
    /// </summary>
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

    /// <summary>
    /// Проверяет, что текущее соединение и <paramref name="targetConnectionId"/> образуют
    /// зарегистрированную сессионную пару в Redis. При неудаче выбрасывает <see cref="HubException"/>.
    /// Защищает relay-методы от использования посторонними соединениями.
    /// </summary>
    private async Task AssertValidSessionAsync(string methodName, string targetConnectionId)
    {
        if (!await _sessions.IsValidSessionPairAsync(Context.ConnectionId, targetConnectionId))
        {
            _logger.LogWarning("{Method} rejected: invalid session pair {From} ↔ {To}",
                methodName, Context.ConnectionId, targetConnectionId);
            throw new HubException("Invalid or expired session.");
        }
    }

    /// <summary>
    /// Извлекает тип ICE-кандидата из строки кандидата (host / srflx / relay) и логирует его.
    /// Используется для мониторинга качества NAT-traversal.
    /// </summary>
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

    /// <summary>
    /// Возвращает UserId из <c>Context.Items</c> (после <c>RegisterDevice</c>) или из JWT-клейма.
    /// </summary>
    private Guid GetUserId()
    {
        if (Context.Items.TryGetValue("UserId", out var cached) && cached is Guid cachedId)
            return cachedId;
        var sub = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Возвращает DeviceId из <c>Context.Items</c> или <c>null</c>, если <c>RegisterDevice</c> не вызывался.
    /// </summary>
    private Guid? GetDeviceId()
        => Context.Items.TryGetValue("DeviceId", out var val) && val is Guid g ? g : null;

    /// <summary>Формирует имя SignalR-группы для папки: <c>folder:{folderId}</c>.</summary>
    private static string FolderGroupKey(Guid folderId) => $"folder:{folderId}";

    /// <summary>Добавляет папку в локальный список папок, которыми владеет данное соединение.</summary>
    private void TrackOwnerFolder(Guid folderId)
    {
        if (!Context.Items.TryGetValue("OwnerFolders", out var existing))
            Context.Items["OwnerFolders"] = new HashSet<Guid> { folderId };
        else
            ((HashSet<Guid>)existing!).Add(folderId);
    }

    /// <summary>Удаляет папку из локального списка owned-папок соединения.</summary>
    private void RemoveOwnerFolder(Guid folderId)
    {
        if (Context.Items.TryGetValue("OwnerFolders", out var existing))
            ((HashSet<Guid>)existing!).Remove(folderId);
    }

    /// <summary>Возвращает копию списка папок, которыми владеет данное соединение.</summary>
    private IEnumerable<Guid> GetOwnerFolders()
    {
        if (Context.Items.TryGetValue("OwnerFolders", out var existing))
            return new List<Guid>((HashSet<Guid>)existing!);
        return [];
    }

}
