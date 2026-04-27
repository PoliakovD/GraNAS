using System.Collections.Concurrent;
using System.Text;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using SIPSorcery.Net;

namespace GraNAS.Desktop.App.Services.P2P;

public sealed class P2PHost : IP2PHost, IAsyncDisposable
{
    private readonly IFolderShareRegistry _registry;
    private readonly IAuthSession _session;
    private readonly ISignalingApi _signalingApi;
    private readonly INotificationService _notifications;
    private readonly string _hubUrl;

    private HubConnection? _hub;
    private readonly ConcurrentDictionary<string, PeerSession> _peers = new();
    private TurnCredentials? _turnCredentials;
    private bool _isOnline;

    public bool IsOnline => _isOnline;
    public bool ShouldBeOnline { get; set; } = true;

    public P2PHost(
        IFolderShareRegistry registry,
        IAuthSession session,
        ISignalingApi signalingApi,
        INotificationService notifications,
        string hubUrl)
    {
        _registry = registry;
        _session = session;
        _signalingApi = signalingApi;
        _notifications = notifications;
        _hubUrl = hubUrl;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_hub?.State == HubConnectionState.Connected) return;

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

        _hub.Reconnected += async _ =>
        {
            _turnCredentials = await _signalingApi.GetTurnCredentialsAsync();
            await JoinAllFoldersAsync();
        };

        await _hub.StartAsync(ct);
        _isOnline = true;
        await JoinAllFoldersAsync(ct);
        Log.Information("P2PHost connected to signaling hub");
    }

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

    private async Task JoinAllFoldersAsync(CancellationToken ct = default)
    {
        foreach (var folderId in _registry.GetAll().Keys)
            await JoinFolderAsync(folderId, ct);
    }

    private async void HandleIncomingPeerRequestAsync(string receiverConnId, Guid folderId, string? scopePath)
    {
        var localPath = _registry.GetLocalPath(folderId);
        if (localPath is null || !Directory.Exists(localPath))
        {
            Log.Warning("Incoming peer request for unmapped/missing folder {FolderId}", folderId);
            return;
        }

        try
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
            pc.setLocalDescription(offer);

            if (_hub?.State == HubConnectionState.Connected)
                await _hub.InvokeAsync("SendOffer", receiverConnId, offer.sdp);

            Log.Information("Offer sent to receiver {ConnId}", receiverConnId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling incoming peer request from {ConnId}", receiverConnId);
            _peers.TryRemove(receiverConnId, out _);
        }
    }

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

    private async Task HandleFileRequestAsync(PeerSession session, string relativePath)
    {
        // Prevent path traversal
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

        if (session.Ecdh?.IsReady == true)
        {
            // Encrypted transfer: send header with IV, then encrypted chunks
            await foreach (var chunk in FileChunker.ReadChunksAsync(safePath))
            {
                var (encrypted, ivBase64) = session.Ecdh.EncryptWithIv(chunk);

                // First chunk: send header first
                if (ivBase64 is not null)
                {
                    SendText(session, ProtocolSerializer.Serialize(
                        new FileHeaderMessage(ProtocolMessageType.FileHeader,
                            relativePath, size, sha256, ivBase64)));
                }

                SendBinary(session, encrypted);
            }
        }
        else
        {
            // Unencrypted fallback (ECDH not completed yet)
            SendText(session, ProtocolSerializer.Serialize(
                new FileHeaderMessage(ProtocolMessageType.FileHeader,
                    relativePath, size, sha256, string.Empty)));

            await foreach (var chunk in FileChunker.ReadChunksAsync(safePath))
                SendBinary(session, chunk);
        }

        SendText(session, ProtocolSerializer.Serialize(
            new FileCompleteMessage(ProtocolMessageType.FileComplete, relativePath)));

        Log.Information("File served: {Path} ({Size} bytes)", relativePath, size);
    }

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

    private sealed class PeerSession(Guid folderId, string localPath, string? scopePath) : IDisposable
    {
        public Guid FolderId { get; } = folderId;
        public string LocalPath { get; } = localPath;
        public string? ScopePath { get; } = scopePath;
        public RTCPeerConnection? Pc { get; set; }
        public RTCDataChannel? DataChannel { get; set; }
        public EcdhSession? Ecdh { get; set; }

        public void Dispose()
        {
            Pc?.close();
            Ecdh?.Dispose();
        }
    }
}
