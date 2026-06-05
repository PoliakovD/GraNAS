using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    private readonly SemaphoreSlim _connectLock = new(1, 1);
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
        if (!await _connectLock.WaitAsync(0, ct)) return;
        try
        {
            if (_hub?.State is HubConnectionState.Connected
                            or HubConnectionState.Connecting
                            or HubConnectionState.Reconnecting) return;

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
                    opts.AccessTokenProvider = async () =>
                    {
                        if (IsTokenNearExpiry(_session.AccessToken))
                        {
                            try { await _session.RefreshAsync(); }
                            catch (Exception ex) { Log.Warning(ex, "Token refresh failed in AccessTokenProvider"); }
                        }
                        return _session.AccessToken;
                    };
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
        finally
        {
            _connectLock.Release();
        }
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
            if (ex.Message.Contains("Not authorized as owner", StringComparison.OrdinalIgnoreCase))
            {
                _registry.RemoveMapping(folderId);
                Log.Information("Removed stale binding for folder {FolderId} (not owned by current user)", folderId);
            }
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
        foreach (var (folderId, localPath) in _registry.GetAll())
        {
            if (Directory.Exists(localPath))
                await JoinFolderAsync(folderId, ct);
            else
                Log.Warning("Skipping JoinAsOwner for folder {FolderId}: local path not found ({LocalPath})", folderId, localPath);
        }
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
            if (iceCandidate.candidate is null)
            {
                Log.Information("ICE gathering complete (end-of-candidates) for peer {ConnId}", receiverConnId);
                return;
            }
            var typ = ExtractIceType(iceCandidate.candidate);
            Log.Information("ICE candidate generated: type={IceType} peer={ConnId} candidate={Candidate}",
                typ, receiverConnId, iceCandidate.candidate);

            // When TURN is available, skip private host candidates: the receiver (web/mobile)
            // would waste 15+ seconds retransmitting to unreachable VPN/LAN IPs before trying relay.
            if (typ == "host" && _turnCredentials != null && IsUnreachableHostCandidate(iceCandidate.candidate))
            {
                Log.Debug("Skipping VPN/tunnel host candidate for peer {ConnId} (TURN available)", receiverConnId);
                return;
            }

            if (_hub?.State != HubConnectionState.Connected) return;
            try
            {
                await _hub.InvokeAsync("SendIceCandidate",
                    receiverConnId,
                    iceCandidate.candidate,
                    iceCandidate.sdpMid,
                    (int?)iceCandidate.sdpMLineIndex);
            }
            catch (Exception ex) { Log.Warning(ex, "ICE send failed for peer {ConnId}", receiverConnId); }
        };

        pc.onicegatheringstatechange += state =>
            Log.Information("ICE gathering state → {State} for peer {ConnId}", state, receiverConnId);

        pc.oniceconnectionstatechange += state =>
            Log.Information("ICE connection state → {State} for peer {ConnId}", state, receiverConnId);

        var offer = pc.createOffer(null);
        await pc.setLocalDescription(offer);
        Log.Information("Offer created (sdpLength={Length}), sending to receiver {ConnId}", offer.sdp?.Length ?? 0, receiverConnId);

        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("SendOffer", receiverConnId, offer.sdp);

        Log.Information("Offer sent to receiver {ConnId}", receiverConnId);
    }

    /// <summary>Применяет SDP-answer от receiver'а к соответствующей <c>RTCPeerConnection</c>.</summary>
    private async Task HandleAnswerAsync(string senderConnId, string sdp)
    {
        if (!_peers.TryGetValue(senderConnId, out var session))
        {
            Log.Warning("Answer from {ConnId} ignored — no active peer session found", senderConnId);
            return;
        }
        try
        {
            Log.Information("Answer received from peer {ConnId} (sdpLength={Length})", senderConnId, sdp.Length);
            session.Pc!.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = sdp
            });
            Log.Information("Remote description set for peer {ConnId}", senderConnId);
        }
        catch (Exception ex) { Log.Warning(ex, "HandleAnswer failed for {ConnId}", senderConnId); }
    }

    /// <summary>Добавляет ICE-кандидата от receiver'а в соответствующую <c>RTCPeerConnection</c>.</summary>
    private async Task HandleIceCandidateAsync(string senderConnId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        if (!_peers.TryGetValue(senderConnId, out var session)) return;
        var typ = ExtractIceType(candidate);
        Log.Information("ICE candidate received from peer {ConnId}: type={IceType}", senderConnId, typ);

        // Chrome anonymizes host candidates with mDNS names (*.local).
        // SIPSorcery's multicast mDNS resolver fails on Windows; use the OS DNS client instead,
        // which resolves Chrome-registered mDNS names via LLMNR/mDNS on the same machine.
        var resolvedCandidate = await ResolveMdnsCandidateAsync(candidate);
        if (resolvedCandidate is null)
        {
            Log.Warning("Skipping unresolvable mDNS candidate from peer {ConnId}: {Candidate}", senderConnId, candidate);
            return;
        }

        try
        {
            session.Pc!.addIceCandidate(new RTCIceCandidateInit
            {
                candidate = resolvedCandidate,
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
            Log.Information("ICE servers configured: STUN=stun.l.google.com:19302, TURN uris=[{TurnUris}] user={TurnUser}",
                string.Join(", ", _turnCredentials.Uris), _turnCredentials.Username);
        }
        else
        {
            Log.Warning("No TURN credentials available — using STUN only");
        }

        return new RTCPeerConnection(config);
    }

    // Filters host candidates that are unreachable from the internet (VPN/tunnel IPs).
    // We keep 192.168.x.x so peers on the same LAN can still use direct host-host ICE.
    // 10.x.x.x is filtered because it's typically a VPN tunnel IP — Chrome would waste
    // 15+ seconds trying relay→10.x.x.x before falling back to relay-relay.
    private static bool IsUnreachableHostCandidate(string candidate)
    {
        var parts = candidate.Split(' ');
        if (parts.Length < 6) return false;
        if (!IPAddress.TryParse(parts[4], out var ip)) return false;
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] == 10                                           // VPN/tunnel (10.x.x.x)
            || b[0] == 127                                          // loopback
            || (b[0] == 169 && b[1] == 254);                       // link-local
    }

    private static string ExtractIceType(string? candidate)
    {
        if (candidate is null) return "null";
        var idx = candidate.IndexOf(" typ ", StringComparison.Ordinal);
        if (idx < 0) return "unknown";
        var rest = candidate[(idx + 5)..];
        return rest.Split(' ')[0];
    }

    /// <summary>
    /// Если ICE-кандидат содержит mDNS-hostname (*.local), пытается разрешить его двумя способами:
    /// 1. System.Net.Dns (работает без VPN, использует Windows DNS/LLMNR)
    /// 2. Прямой multicast mDNS-запрос на физических интерфейсах (работает когда VPN меняет DNS).
    /// Chrome регистрирует mDNS-имена на физическом интерфейсе — ответ на запрос 224.0.0.251:5353
    /// приходит от самого Chrome на той же машине.
    /// </summary>
    private static async Task<string?> ResolveMdnsCandidateAsync(string candidate)
    {
        var parts = candidate.Split(' ');
        if (parts.Length < 6) return candidate;

        var address = parts[4];
        if (!address.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return candidate;

        // Phase 1: system DNS (works without VPN)
        try
        {
            var addrs = await System.Net.Dns.GetHostAddressesAsync(address);
            var ipv4 = Array.Find(addrs, a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4 is not null)
            {
                Log.Debug("Resolved mDNS {Host} → {Ip} (system DNS)", address, ipv4);
                parts[4] = ipv4.ToString();
                return string.Join(' ', parts);
            }
        }
        catch { /* fall through to multicast */ }

        // Phase 2: raw multicast mDNS on physical interfaces (works when VPN overrides DNS)
        var ip = await QueryMdnsMulticastAsync(address);
        if (ip is not null)
        {
            Log.Debug("Resolved mDNS {Host} → {Ip} (multicast)", address, ip);
            parts[4] = ip.ToString();
            return string.Join(' ', parts);
        }

        Log.Warning("Could not resolve mDNS candidate {Host} — skipping", address);
        return null;
    }

    /// <summary>
    /// Отправляет DNS-запрос типа A на адрес mDNS-multicast (224.0.0.251:5353) через все
    /// физические сетевые интерфейсы (не VPN, не loopback). Chrome отвечает на запросы
    /// для своих зарегистрированных *.local имён с той же машины.
    /// </summary>
    private static async Task<IPAddress?> QueryMdnsMulticastAsync(string mdnsName)
    {
        var mcastEp = new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353);
        var query = BuildDnsQuery(mdnsName);

        var physicalIps = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                         && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(a.Address))
            .Select(a => a.Address)
            .ToList();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        foreach (var localIp in physicalIps)
        {
            try
            {
                using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                sock.Bind(new IPEndPoint(localIp, 0));
                sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                    new MulticastOption(IPAddress.Parse("224.0.0.251"), localIp));
                sock.MulticastLoopback = true;
                sock.ReceiveTimeout = 2000;
                sock.SendTo(query, mcastEp);

                var buf = new byte[4096];
                while (!cts.IsCancellationRequested)
                {
                    int received;
                    try { received = await Task.Run(() => sock.Receive(buf), cts.Token); }
                    catch { break; }
                    var ip = ParseDnsARecord(buf, received, mdnsName);
                    if (ip is not null) return ip;
                }
            }
            catch (Exception ex) { Log.Debug(ex, "mDNS multicast failed on interface {Ip}", localIp); }
        }
        return null;
    }

    private static byte[] BuildDnsQuery(string name)
    {
        using var ms = new MemoryStream();
        // DNS header (big-endian)
        ms.Write(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 }); // ID=0, Flags=query, QDCOUNT=1
        // QNAME
        foreach (var label in name.TrimEnd('.').Split('.'))
        {
            var b = Encoding.UTF8.GetBytes(label);
            ms.WriteByte((byte)b.Length);
            ms.Write(b);
        }
        ms.WriteByte(0);               // end of QNAME
        ms.Write(new byte[] { 0, 1 }); // QTYPE = A
        ms.Write(new byte[] { 0, 1 }); // QCLASS = IN
        return ms.ToArray();
    }

    private static IPAddress? ParseDnsARecord(byte[] buf, int length, string queryName)
    {
        if (length < 12) return null;
        int anCount = (buf[6] << 8) | buf[7];
        if (anCount == 0) return null;

        int pos = 12;
        int qdCount = (buf[4] << 8) | buf[5];

        // Skip questions
        for (int q = 0; q < qdCount && pos < length; q++)
        {
            while (pos < length)
            {
                if ((buf[pos] & 0xC0) == 0xC0) { pos += 2; break; }
                if (buf[pos] == 0) { pos++; break; }
                pos += 1 + buf[pos];
            }
            pos += 4; // QTYPE + QCLASS
        }

        // Parse answer records
        for (int a = 0; a < anCount && pos < length; a++)
        {
            // Skip answer name (pointer or labels)
            if ((buf[pos] & 0xC0) == 0xC0) pos += 2;
            else { while (pos < length && buf[pos] != 0) pos += 1 + buf[pos]; pos++; }

            if (pos + 10 > length) break;
            int type = (buf[pos] << 8) | buf[pos + 1];
            int rdLen = (buf[pos + 8] << 8) | buf[pos + 9];
            pos += 10;

            if (type == 1 && rdLen == 4 && pos + 4 <= length) // A record
                return new IPAddress(new[] { buf[pos], buf[pos + 1], buf[pos + 2], buf[pos + 3] });

            pos += rdLen;
        }
        return null;
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

    private static bool IsTokenNearExpiry(string? token)
    {
        if (token is null) return true;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.ValidTo < DateTime.UtcNow.AddMinutes(1);
        }
        catch { return true; }
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
