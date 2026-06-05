import type { HubConnection } from '@microsoft/signalr';
import { createHubConnection } from './signalingClient';
import {
  deriveAesKey, decryptChunk, exportPublicKeyBase64,
  generateEcdhKeyPair, importPublicKey, sha256Hex,
} from './ecdhUtils';
import { MsgType, parseMsg } from './protocol';
import type { RemoteFileEntry, SessionStatus } from './types';
import type { TurnCredentials } from '../api/signaling.api';
import { p2pDebug } from './p2pDebug'; // TEMP debug — remove with P2PDebugLog

// TEMP debug helper: extract "typ <x>" + ip:port from an ICE candidate string.
function describeCandidate(c: string): string {
  const typ = /\btyp (\w+)/.exec(c)?.[1] ?? '?';
  const parts = c.split(' ');
  const ipPort = parts.length >= 6 ? `${parts[4]}:${parts[5]}` : c;
  return `${typ} ${ipPort}`;
}

const STUN: RTCIceServer = { urls: 'stun:stun.l.google.com:19302' };

export interface P2PSessionCallbacks {
  onStatusChange: (s: SessionStatus) => void;
  onFiles: (files: RemoteFileEntry[]) => void;
  onDownloadProgress: (path: string, received: number, total: number) => void;
  onDownloadDone: (path: string, blob: Blob) => void;
  onError: (msg: string) => void;
}

export interface P2PSession {
  connect(): Promise<void>;
  requestFiles(): void;
  downloadFile(path: string): void;
  disconnect(): void;
}

type DownloadState = {
  path: string;
  sha256: string;
  total: number;
  chunks: Uint8Array[];
  received: number;
};

export function createP2PSession(
  folderId: string,
  shareToken: string | undefined,
  turnCredentials: TurnCredentials | null,
  callbacks: P2PSessionCallbacks,
): P2PSession {
  const hub: HubConnection = createHubConnection();
  let pc: RTCPeerConnection | null = null;
  let dc: RTCDataChannel | null = null;
  let ecdhKeyPair: CryptoKeyPair | null = null;
  let aesKey: CryptoKey | null = null;
  let downloadState: DownloadState | null = null;
  // Buffer ICE candidates that arrive before pc + remote description are ready (trickle/offer race).
  let remoteReady = false;
  const pendingCandidates: { candidate: string; sdpMid: string | null; sdpMLineIndex: number | null }[] = [];

  // ---- Hub signaling ----

  async function requestSession(): Promise<void> {
    callbacks.onStatusChange('negotiating');
    p2pDebug.log('→ RequestSession у signaling');
    try {
      await hub.invoke('RequestSession', folderId, shareToken ?? null);
    } catch {
      callbacks.onStatusChange('error');
      callbacks.onError('Не удалось запросить сессию у signaling-сервера');
      p2pDebug.log('✗ RequestSession failed');
    }
  }

  async function handleOffer(senderConnId: string, sdp: string): Promise<void> {
    const iceServers: RTCIceServer[] = [STUN];
    if (turnCredentials) {
      for (const uri of turnCredentials.uris) {
        iceServers.push({ urls: uri, username: turnCredentials.username, credential: turnCredentials.credential });
      }
    }

    p2pDebug.log(`offer получен (sdpLen=${sdp.length}); TURN=${turnCredentials ? turnCredentials.uris.join(',') : 'нет (только STUN)'}`);
    pc = new RTCPeerConnection({ iceServers });

    pc.onicecandidate = async (e) => {
      if (!e.candidate) { p2pDebug.log('локальные кандидаты собраны (end-of-candidates)'); return; }
      p2pDebug.log(`локальный кандидат: ${describeCandidate(e.candidate.candidate)}`);
      try {
        await hub.invoke('SendIceCandidate',
          senderConnId, e.candidate.candidate, e.candidate.sdpMid, e.candidate.sdpMLineIndex);
      } catch { /* non-fatal */ }
    };

    pc.onicecandidateerror = (e) => {
      const ev = e as RTCPeerConnectionIceErrorEvent;
      p2pDebug.log(`⚠ ICE candidate error: url=${ev.url} code=${ev.errorCode} ${ev.errorText}`);
    };

    pc.onconnectionstatechange = () => {
      p2pDebug.log(`connection state: ${pc?.connectionState}`);
      if (pc?.connectionState === 'failed') {
        callbacks.onStatusChange('error');
        callbacks.onError('ICE connection failed — check TURN server or network reachability');
      }
    };

    pc.oniceconnectionstatechange = () => {
      p2pDebug.log(`ICE state: ${pc?.iceConnectionState}`);
    };

    pc.onicegatheringstatechange = () => {
      p2pDebug.log(`ICE gathering: ${pc?.iceGatheringState}`);
    };

    pc.ondatachannel = (e) => { p2pDebug.log('datachannel получен'); attachDataChannel(e.channel); };

    await pc.setRemoteDescription({ type: 'offer', sdp });
    remoteReady = true;
    p2pDebug.log('remote offer установлен');
    flushPendingCandidates();
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    await hub.invoke('SendAnswer', senderConnId, answer.sdp ?? '');
    p2pDebug.log(`answer отправлен (sdpLen=${answer.sdp?.length ?? 0})`);
  }

  // SIPSorcery sends trickled candidates WITHOUT the "candidate:" prefix that Chrome's
  // addIceCandidate requires (offer-SDP candidates have it, trickled ones don't) — and emits
  // srflx/relay with a bogus "raddr 0.0.0.0 rport 0". Both make Chrome throw "Error processing
  // ICE candidate". Add the prefix and strip the invalid related-address.
  function sanitizeCandidate(c: string): string {
    let s = c.trim();
    if (s && !s.startsWith('candidate:')) s = `candidate:${s}`;
    s = s.replace(/\s+raddr\s+(?:0\.0\.0\.0|::)\s+rport\s+0\b/i, '');
    return s;
  }

  function addCandidate(candidate: string, sdpMid: string | null, sdpMLineIndex: number | null): void {
    if (!pc) return;
    const fixed = sanitizeCandidate(candidate);
    pc.addIceCandidate({
      candidate: fixed,
      sdpMid: sdpMid ?? undefined,
      sdpMLineIndex: sdpMLineIndex ?? undefined,
    })
      .then(() => p2pDebug.log(`  ✓ кандидат добавлен: ${describeCandidate(fixed)}`))
      .catch((err: unknown) => p2pDebug.log(`✗ addIceCandidate REJECT [${describeCandidate(fixed)}]: ${String(err)}`));
  }

  function flushPendingCandidates(): void {
    if (pendingCandidates.length) p2pDebug.log(`применяю ${pendingCandidates.length} буфер. кандидат(ов)`);
    for (const c of pendingCandidates) addCandidate(c.candidate, c.sdpMid, c.sdpMLineIndex);
    pendingCandidates.length = 0;
  }

  function handleIceCandidate(candidate: string, sdpMid: string | null, sdpMLineIndex: number | null): void {
    p2pDebug.log(`remote кандидат: ${describeCandidate(candidate)}`);
    if (!pc || !remoteReady) {
      pendingCandidates.push({ candidate, sdpMid, sdpMLineIndex });
      p2pDebug.log('  (в буфер — pc/remote ещё не готовы)');
      return;
    }
    addCandidate(candidate, sdpMid, sdpMLineIndex);
  }

  // ---- Data channel ----

  function attachDataChannel(channel: RTCDataChannel): void {
    dc = channel;
    dc.binaryType = 'arraybuffer';
    dc.onopen = () => { p2pDebug.log('✓ datachannel OPEN'); void initiateEcdh(); };
    dc.onmessage = (e) => {
      if (typeof e.data === 'string') handleTextMessage(e.data);
      else void handleBinaryChunk(new Uint8Array(e.data as ArrayBuffer));
    };
    dc.onerror = () => {
      callbacks.onStatusChange('error');
      callbacks.onError('Data channel error');
      p2pDebug.log('✗ datachannel error');
    };
    // Channel may already be open when ondatachannel fires (race with ICE)
    if (dc.readyState === 'open') { p2pDebug.log('✓ datachannel уже OPEN'); void initiateEcdh(); }
  }

  async function initiateEcdh(): Promise<void> {
    callbacks.onStatusChange('ecdh');
    p2pDebug.log('ECDH: отправка публичного ключа');
    ecdhKeyPair = await generateEcdhKeyPair();
    const pubKeyB64 = await exportPublicKeyBase64(ecdhKeyPair.publicKey);
    dc?.send(JSON.stringify({ type: MsgType.EcdhOffer, publicKey: pubKeyB64 }));
  }

  function handleTextMessage(json: string): void {
    const msg = parseMsg(json);
    if (!msg) return;

    switch (msg.type) {
      case MsgType.EcdhAnswer:
        void handleEcdhAnswer(msg.publicKey).then(() => {
          callbacks.onStatusChange('ready');
          requestFiles();
        });
        break;
      case MsgType.ListResponse:
        p2pDebug.log(`✓ список файлов получен: ${msg.files.length}`);
        callbacks.onFiles(msg.files);
        callbacks.onStatusChange('ready');
        break;
      case MsgType.FileHeader:
        downloadState = { path: msg.path, sha256: msg.sha256, total: msg.size, chunks: [], received: 0 };
        break;
      case MsgType.FileComplete:
        void finalizeDownload();
        break;
      case MsgType.Error:
        callbacks.onError(`${msg.code}: ${msg.message}`);
        callbacks.onStatusChange('ready');
        break;
    }
  }

  async function handleEcdhAnswer(peerPublicKeyBase64: string): Promise<void> {
    if (!ecdhKeyPair) return;
    const peerKey = await importPublicKey(peerPublicKeyBase64);
    aesKey = await deriveAesKey(ecdhKeyPair.privateKey, peerKey);
  }

  async function handleBinaryChunk(data: Uint8Array): Promise<void> {
    if (!downloadState) return;
    let chunk: Uint8Array;
    if (aesKey) {
      try { chunk = await decryptChunk(aesKey, data); }
      catch { callbacks.onError('Decryption failed'); downloadState = null; return; }
    } else {
      chunk = data;
    }
    downloadState.chunks.push(chunk);
    downloadState.received += chunk.length;
    callbacks.onDownloadProgress(downloadState.path, downloadState.received, downloadState.total);
  }

  async function finalizeDownload(): Promise<void> {
    if (!downloadState) return;
    const state = downloadState;
    downloadState = null;

    const totalLen = state.chunks.reduce((acc, c) => acc + c.length, 0);
    const combined = new Uint8Array(totalLen);
    let offset = 0;
    for (const chunk of state.chunks) { combined.set(chunk, offset); offset += chunk.length; }

    const actualHash = await sha256Hex(combined);
    if (actualHash !== state.sha256) {
      callbacks.onError(`SHA-256 mismatch for ${state.path}. File may be corrupted.`);
    } else {
      callbacks.onDownloadDone(state.path, new Blob([combined]));
    }
    callbacks.onStatusChange('ready');
  }

  function requestFiles(): void {
    if (dc?.readyState === 'open')
      dc.send(JSON.stringify({ type: MsgType.ListRequest }));
  }

  function downloadFile(path: string): void {
    if (dc?.readyState === 'open') {
      callbacks.onStatusChange('downloading');
      dc.send(JSON.stringify({ type: MsgType.FileRequest, path }));
    }
  }

  // ---- Public session API ----

  return {
    async connect() {
      callbacks.onStatusChange('connecting');
      p2pDebug.log(`=== connect() folder=${folderId} ===`);
      hub.on('Offer', (senderConnId: string, sdp: string) => {
        p2pDebug.log(`Offer event от ${senderConnId.slice(0, 8)}`);
        handleOffer(senderConnId, sdp).catch(err => {
          callbacks.onStatusChange('error');
          callbacks.onError(`P2P handshake failed: ${err}`);
        });
      });
      hub.on('IceCandidate', (_: string, candidate: string, sdpMid: string | null, sdpMLineIndex: number | null) =>
        handleIceCandidate(candidate, sdpMid, sdpMLineIndex));
      hub.on('OwnerOnlineStatusChanged', () => { /* handled by useOwnerOnlineStatus */ });
      hub.on('AccessDenied', (_folderId: string, reason: string) => {
        callbacks.onStatusChange('error');
        callbacks.onError(`Access denied: ${reason}`);
        p2pDebug.log(`✗ AccessDenied: ${reason}`);
      });
      hub.on('OwnerOffline', () => {
        callbacks.onStatusChange('error');
        callbacks.onError('Owner went offline');
        p2pDebug.log('✗ OwnerOffline');
      });
      await hub.start();
      p2pDebug.log('hub подключён');
      await hub.invoke('WatchFolder', folderId);
      await requestSession();
    },

    requestFiles,
    downloadFile,

    disconnect() {
      pc?.close();
      pc = null;
      dc = null;
      void hub.stop();
      callbacks.onStatusChange('idle');
    },
  };
}
