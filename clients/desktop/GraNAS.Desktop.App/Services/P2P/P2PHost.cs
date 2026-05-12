using System.Collections.Concurrent;
using System.Text;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using SIPSorcery.Net;

namespace GraNAS.Desktop.App.Services.P2P;

/// <summary>
/// Реализация P2P-хоста для desktop-клиента. Управляет SignalR-подключением к хабу сигналинга,
/// обрабатывает входящие P2P-запросы от receiver'ов и отдаёт файлы через WebRTC data channel.
/// </summary>
/// <remarks>
/// Жизненный цикл соединения:
/// <list type="number">
/// <item><c>ConnectAsync</c>: регистрация устройства в REST API (единожды per user) → получение TURN-кред → <c>RegisterDevice</c> в хабе → <c>JoinAsOwner</c> для всех папок из реестра.</item>
/// <item>При получении <c>IncomingPeerRequest</c>: создаётся <c>RTCPeerConnection</c> + data channel, генерируется SDP-offer → <c>SendOffer</c> в хаб.</item>
/// <item>После установки data channel: ECDH-рукопожатие → <c>list_request</c>/<c>file_request</c> → передача зашифрованных чанков.</item>
/// <item>При переподключении: автоматически re-issue TURN + повторный <c>RegisterDevice</c> + <c>JoinAsOwner</c> для всех папок.</item>
/// <item><c>ForceDisconnect</c> от сервера: устанавливает <c>ShouldBeOnline=false</c> и вызывает <c>DisconnectAsync</c>.</item>
/// </list>
/// </remarks>
public class P2PHost : IP2PHost, IAsyncDisposable
{
    private readonly IFolderShareRegistry _registry;
    private readonly IAuthSession _session;
    private readonly ISignalingApi _signalingApi;
    private readonly IDeviceIdentity _deviceIdentity;
    private readonly INotificationService _notifications;
    private readonly string _hubUrl;

    private HubConnection? _hub;
    private readonly ConcurrentDictionary<string, PeerSession> _peers = new();
    private readonly ConcurrentDictionary<Guid, Guid?> _bindingCache = new();
    private TurnCredentials? _turnCredentials;
    private bool _isOnline;

    public bool IsOnline => _isOnline;
    public bool ShouldBeOnline { get; set; } = true;

    public P2PHost(
        IFolderShareRegistry registry,
        IAuthSession session,
        ISignalingApi signalingApi,
        IDeviceIdentity deviceIdentity,
        INotificationService notifications,
        string hubUrl)
    {
        _registry = registry;
        _session = session;
        _signalingApi = signalingApi;
        _deviceIdentity = deviceIdentity;
        _notifications = notifications;
        _hubUrl = hubUrl;
        _registry.MappingChanged += folderId => _bindingCache.TryRemove(folderId, out _);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// При первом подключении данного пользователя регистрирует устройство через REST API.
    /// Получает TURN-учётные данные, строит <c>HubConnection</c> с JWT в AccessTokenProvider
    /// и подписывается на события хаба.
    /// </remarks>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_hub?.State == HubConnectionState.Connected) return;

        if (!_deviceIdentity.IsRegisteredForUser(_session.CurrentUserId))
        {
            await _signalingApi.RegisterDeviceAsync(
                new DeviceRegistrationRequest(_deviceIdentity.DeviceId, _deviceIdentity.DeviceName, _deviceIdentity.Platform), ct);
            _deviceIdentity.MarkRegisteredForUser(_session.CurrentUserId);
        }

        _turnCredentials = await _signalingApi.GetTurnCredentialsAsync(ct);

        _hub = new HubConnectionBuilder()
            .WithUrl(_hubUrl, opts =>
            {
                opts.AccessTokenProvider = () => Task.FromResult<string?>(_session.AccessToken);
                opts.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<string, Guid, string?>("IncomingPeerRequest", HandleIncomingPeerRequestAsync);
        _hub.On<string, string>("Answer", HandleAnswerAsync);
        _hub.On<string, string, string?, int?>("IceCandidate", HandleIceCandidateAsync);
        _hub.On("ForceDisconnect", HandleForceDisconnectAsync);

        _hub.Reconnected += async _ =>
        {
            _turnCredentials = await _signalingApi.GetTurnCredentialsAsync();
            await RegisterDeviceInHubAsync();
            await JoinAllFoldersAsync();
        };

        await _hub.StartAsync(ct);
        await RegisterDeviceInHubAsync(ct);
        _isOnline = true;
        await JoinAllFoldersAsync(ct);
        Log.Information("P2PHost connected to signaling hub (device {DeviceId})", _deviceIdentity.DeviceId);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync()
    {
        _isOnline = false;
        if (_hub is not null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }
        foreach (var peer in _peers.Values)
            peer.Dispose();
        _peers.Clear();
        _bindingCache.Clear();
        Log.Information("P2PHost disconnected");
    }

    public async Task JoinFolderAsync(Guid folderId, CancellationToken ct = default)
    {
        if (_hub?.State != HubConnectionState.Connected) return;
        try
        {
            await _hub.InvokeAsync("JoinAsOwner", folderId, ct);
            Log.Information("P2PHost joined folder {FolderId} as owner", folderId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "JoinAsOwner failed for folder {FolderId}", folderId);
        }
    }

    public async Task LeaveFolderAsync(Guid folderId)
    {
        if (_hub?.State != HubConnectionState.Connected) return;
        try { await _hub.InvokeAsync("LeaveAsOwner", folderId); }
        catch (Exception ex) { Log.Warning(ex, "LeaveAsOwner failed for folder {FolderId}", folderId); }
    }

    /// <summary>
    /// Вызывает <c>RegisterDevice(deviceId)</c> в SignalR-хабе.
    /// Должен вызываться сразу после <c>StartAsync</c> и при каждом переподключении.
    /// </summary>
    private async Task RegisterDeviceInHubAsync(CancellationToken ct = default)
    {
        if (_hub?.State != HubConnectionState.Connected) return;
        try
        {
            await _hub.InvokeAsync("RegisterDevice", _deviceIdentity.DeviceId, ct);
            Log.Information("Device {DeviceId} registered in hub", _deviceIdentity.DeviceId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "RegisterDevice failed for device {DeviceId}", _deviceIdentity.DeviceId);
        }
    }

    /// <summary>
    /// Обрабатывает событие <c>ForceDisconnect</c> от сервера.
    /// Сбрасывает <c>ShouldBeOnline</c> в <c>false</c>, чтобы подавить автопереподключение,
    /// уведомляет пользователя и вызывает <c>DisconnectAsync</c>.
    /// </summary>
    private async void HandleForceDisconnectAsync()
    {
        Log.Warning("ForceDisconnect received from server — disconnecting");
        ShouldBeOnline = false;
        _notifications.Info("Ваша сессия была принудительно завершена.", "Сессия завершена");
        await DisconnectAsync();
    }

    private async Task JoinAllFoldersAsync(CancellationToken ct = default)
    {
        foreach (var folderId in _registry.GetAll().Keys)
            await JoinFolderAsync(folderId, ct);
    }

    /// <summary>
    /// Обрабатывает входящий P2P-запрос от receiver'а.
    /// Проверяет локальное наличие папки и server-side device-folder binding,
    /// затем создаёт <c>RTCPeerConnection</c> + data channel через <see cref="StartWebRtcSessionAsync"/>.
    /// </summary>
    private async void HandleIncomingPeerRequestAsync(string receiverConnId, Guid folderId, string? scopePath)
        => await HandleIncomingPeerRequestCoreAsync(receiverConnId, folderId, scopePath);

    internal async Task HandleIncomingPeerRequestCoreAsync(string receiverConnId, Guid folderId, string? scopePath)
    {
        var localPath = _registry.GetLocalPath(folderId);
        if (localPath is null || !Directory.Exists(localPath))
        {
            Log.Warning("Incoming peer request for unmapped/missing folder {FolderId}", folderId);
            return;
        }

        var boundDeviceId = await ResolveBoundDeviceIdAsync(folderId);
        if (boundDeviceId is { } bound && bound != _deviceIdentity.DeviceId)
        {
            Log.Warning(
                "Folder {FolderId} bound to device {BoundDeviceId}, refusing peer {ConnId}",
                folderId, bound, receiverConnId);
            await SendDenyAsync(receiverConnId, folderId, "folder_bound_to_another_device");
            return;
        }

        try { await StartWebRtcSessionAsync(receiverConnId, folderId, scopePath, localPath); }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling incoming peer request from {ConnId}", receiverConnId);
            _peers.TryRemove(receiverConnId, out _);
        }
    }

    private async Task<Guid?> ResolveBoundDeviceIdAsync(Guid folderId)
    {
        if (_bindingCache.TryGetValue(folderId, out var cached)) return cached;
        var resp = await _signalingApi.GetFolderDevicesAsync(new[] { folderId });
        var deviceId = resp.FirstOrDefault(r => r.FolderId == folderId)?.DeviceId;
        _bindingCache[folderId] = deviceId;
        return deviceId;
    }

    protected internal virtual async Task SendDenyAsync(string receiverConnId, Guid folderId, string reason)
    {
        if (_hub?.State != HubConnectionState.Connected) return;
        try { await _hub.InvokeAsync("DenyPeerRequest", receiverConnId, folderId, reason); }
        catch (Exception ex) { Log.Warning(ex, "DenyPeerRequest hub call failed for {ConnId}", receiverConnId); }
    }

    protected internal virtual async Task StartWebRtcSessionAsync(
        string receiverConnId, Guid folderId, string? scopePath, string localPath)
    {
        var session = new PeerSession(folderId, localPath, scopePath);
        _peers[receiverConnId] = session;

        var pc = CreatePeerConnection();
        session.Pc = pc;

        // Create data channel on owner side — receiver gets it via ondatachannel
        var dc = await pc.createDataChannel("files");
        session.DataChannel = dc;

        dc.onopen += () =>
        {
            Log.Information("Data channel opened to receiver {ConnId}", receiverConnId);
        };

        dc.onmessage += (channel, protocol, data) =>
        {
            var text = Encoding.UTF8.GetString(data);
            _ = HandleDataChannelMessageAsync(session, receiverConnId, text);
        };

        pc.onicecandidate += async iceCandidate =>
        {
            if (_hub?.State != HubConnectionState.Connected) return;
            try
            {
                await _hub.InvokeAsync("SendIceCandidate",
                    receiverConnId,
                    iceCandidate.candidate,
                    iceCandidate.sdpMid,
                    (int?)iceCandidate.sdpMLineIndex);
            }
            catch (Exception ex) { Log.Warning(ex, "ICE send failed"); }
        };

        var offer = pc.createOffer(null);
        await pc.setLocalDescription(offer);

        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("SendOffer", receiverConnId, offer.sdp);

        Log.Information("Offer sent to receiver {ConnId}", receiverConnId);
    }

    /// <summary>Применяет SDP-answer от receiver'а к соответствующей <c>RTCPeerConnection</c>.</summary>
    private async Task HandleAnswerAsync(string senderConnId, string sdp)
    {
        if (!_peers.TryGetValue(senderConnId, out var session)) return;
        try
        {
            session.Pc!.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = sdp
            });
            Log.Debug("Answer set from {ConnId}", senderConnId);
        }
        catch (Exception ex) { Log.Warning(ex, "HandleAnswer failed for {ConnId}", senderConnId); }
    }

    /// <summary>Добавляет ICE-кандидата от receiver'а в соответствующую <c>RTCPeerConnection</c>.</summary>
    private async Task HandleIceCandidateAsync(string senderConnId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        if (!_peers.TryGetValue(senderConnId, out var session)) return;
        try
        {
            session.Pc!.addIceCandidate(new RTCIceCandidateInit
            {
                candidate = candidate,
                sdpMid = sdpMid,
                sdpMLineIndex = (ushort)(sdpMLineIndex ?? 0)
            });
        }
        catch (Exception ex) { Log.Warning(ex, "AddIceCandidate failed for {ConnId}", senderConnId); }
    }

    /// <summary>
    /// Маршрутизирует JSON-сообщение из data channel к соответствующему обработчику
    /// по полю <c>type</c>. Неизвестные типы логируются и игнорируются.
    /// </summary>
    private async Task HandleDataChannelMessageAsync(PeerSession session, string receiverConnId, string json)
    {
        var type = ProtocolSerializer.GetMessageType(json);
        try
        {
            switch (type)
            {
                case ProtocolMessageType.EcdhOffer:
                    await HandleEcdhOfferAsync(session, json);
                    break;

                case ProtocolMessageType.ListRequest:
                    await HandleListRequestAsync(session);
                    break;

                case ProtocolMessageType.FileRequest:
                    var fileReq = ProtocolSerializer.Deserialize<FileRequestMessage>(json);
                    if (fileReq is not null)
                        await HandleFileRequestAsync(session, fileReq.Path);
                    break;

                default:
                    Log.Debug("Unknown data channel message type: {Type}", type);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling data channel message type={Type}", type);
            SendText(session, ProtocolSerializer.Serialize(
                new DataChannelErrorMessage(ProtocolMessageType.Error, "INTERNAL_ERROR", ex.Message)));
        }
    }

    /// <summary>
    /// Обрабатывает <c>ecdh_offer</c> от receiver'а: инициализирует <see cref="EcdhSession"/>,
    /// вычисляет общий ключ и отправляет свой публичный ключ в <c>ecdh_answer</c>.
    /// </summary>
    private Task HandleEcdhOfferAsync(PeerSession session, string json)
    {
        var offer = ProtocolSerializer.Deserialize<EcdhOfferMessage>(json);
        if (offer is null) return Task.CompletedTask;

        session.Ecdh = new EcdhSession();
        var ownerPublicKey = session.Ecdh.GetPublicKeyBase64();
        session.Ecdh.DeriveSharedKey(offer.PublicKey);

        SendText(session, ProtocolSerializer.Serialize(
            new EcdhAnswerMessage(ProtocolMessageType.EcdhAnswer, ownerPublicKey)));

        Log.Debug("ECDH key exchange complete");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Обрабатывает <c>list_request</c>: рекурсивно перечисляет файлы в локальной папке
    /// с учётом <c>ScopePath</c> и отправляет <c>list_response</c> с относительными путями.
    /// </summary>
    private Task HandleListRequestAsync(PeerSession session)
    {
        var entries = new List<RemoteFileEntry>();
        try
        {
            var root = session.LocalPath;
            var searchRoot = session.ScopePath is not null
                ? Path.Combine(root, session.ScopePath.TrimStart('/', '\\'))
                : root;

            if (Directory.Exists(searchRoot))
            {
                foreach (var file in Directory.EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
                    entries.Add(new RemoteFileEntry(relativePath, info.Length, info.LastWriteTimeUtc));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error listing files");
        }

        SendText(session, ProtocolSerializer.Serialize(
            new ListResponseMessage(ProtocolMessageType.ListResponse, [.. entries])));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Обрабатывает <c>file_request</c>: валидирует путь, отправляет <c>file_header</c>
    /// и передаёт зашифрованные чанки в бинарном формате <c>nonce(12) || ciphertext || tag(16)</c>.
    /// При отсутствии завершённого ECDH чанки отправляются без шифрования (fallback).
    /// </summary>
    /// <param name="relativePath">Путь к файлу относительно корня shared-папки.</param>
    private async Task HandleFileRequestAsync(PeerSession session, string relativePath)
    {
        // Защита от path traversal: разрешённый путь должен лежать внутри localPath
        var safePath = Path.GetFullPath(Path.Combine(session.LocalPath, relativePath));
        if (!safePath.StartsWith(session.LocalPath, StringComparison.OrdinalIgnoreCase))
        {
            SendText(session, ProtocolSerializer.Serialize(
                new DataChannelErrorMessage(ProtocolMessageType.Error, "FORBIDDEN", "Path traversal denied.")));
            return;
        }

        if (!File.Exists(safePath))
        {
            SendText(session, ProtocolSerializer.Serialize(
                new DataChannelErrorMessage(ProtocolMessageType.Error, "NOT_FOUND", $"File not found: {relativePath}")));
            return;
        }

        var sha256 = await FileChunker.ComputeSha256HexAsync(safePath);
        var size = FileChunker.GetFileSize(safePath);

        // One header at start; chunks are self-contained (nonce+ciphertext+tag or raw)
        SendText(session, ProtocolSerializer.Serialize(
            new FileHeaderMessage(ProtocolMessageType.FileHeader,
                relativePath, size, sha256, string.Empty)));

        if (session.Ecdh?.IsReady == true)
        {
            // Encrypted: each chunk = nonce(12) + ciphertext + tag(16)
            await foreach (var chunk in FileChunker.ReadChunksAsync(safePath))
                SendBinary(session, session.Ecdh.Encrypt(chunk));
        }
        else
        {
            // Unencrypted fallback (ECDH not yet complete)
            await foreach (var chunk in FileChunker.ReadChunksAsync(safePath))
                SendBinary(session, chunk);
        }

        SendText(session, ProtocolSerializer.Serialize(
            new FileCompleteMessage(ProtocolMessageType.FileComplete, relativePath)));

        Log.Information("File served: {Path} ({Size} bytes)", relativePath, size);
    }

    /// <summary>
    /// Создаёт <c>RTCPeerConnection</c> с конфигурацией ICE-серверов:
    /// публичный STUN (Google) + TURN-сервер из текущих учётных данных.
    /// </summary>
    private RTCPeerConnection CreatePeerConnection()
    {
        var config = new RTCConfiguration();

        // Public STUN
        config.iceServers = [new RTCIceServer { urls = "stun:stun.l.google.com:19302" }];

        // TURN credentials (from signaling-service)
        if (_turnCredentials is not null)
        {
            foreach (var uri in _turnCredentials.Uris)
            {
                config.iceServers.Add(new RTCIceServer
                {
                    urls = uri,
                    username = _turnCredentials.Username,
                    credential = _turnCredentials.Credential,
                    credentialType = RTCIceCredentialType.password
                });
            }
        }

        return new RTCPeerConnection(config);
    }

    private static void SendText(PeerSession session, string text)
    {
        try { session.DataChannel?.send(text); }
        catch (Exception ex) { Log.Warning(ex, "DataChannel send (text) failed"); }
    }

    private static void SendBinary(PeerSession session, byte[] data)
    {
        try { session.DataChannel?.send(data); }
        catch (Exception ex) { Log.Warning(ex, "DataChannel send (binary) failed"); }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    /// <summary>
    /// Состояние одной активной P2P-сессии с конкретным receiver'ом.
    /// Существует с момента <c>IncomingPeerRequest</c> до закрытия соединения.
    /// </summary>
    private sealed class PeerSession(Guid folderId, string localPath, string? scopePath) : IDisposable
    {
        public Guid FolderId { get; } = folderId;
        /// <summary>Абсолютный путь к shared-папке на диске owner'а.</summary>
        public string LocalPath { get; } = localPath;
        /// <summary>Путь-подсказка из <c>IncomingPeerRequest</c>. <c>null</c> = вся папка.</summary>
        public string? ScopePath { get; } = scopePath;
        public RTCPeerConnection? Pc { get; set; }
        public RTCDataChannel? DataChannel { get; set; }
        /// <summary>ECDH-сессия для шифрования файловых чанков. <c>null</c> до завершения рукопожатия.</summary>
        public EcdhSession? Ecdh { get; set; }

        public void Dispose()
        {
            Pc?.close();
            Ecdh?.Dispose();
        }
    }
}
