import type { HubConnection } from '@microsoft/signalr';
import { createHubConnection } from './signalingClient';
import {
  deriveAesKey, decryptChunk, exportPublicKeyBase64,
  generateEcdhKeyPair, importPublicKey, sha256Hex,
} from './ecdhUtils';
import { MsgType, parseMsg } from './protocol';
import type { RemoteFileEntry, SessionStatus } from './types';
import type { TurnCredentials } from '../api/signaling.api';

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

  // ---- Hub signaling ----

  async function requestSession(): Promise<void> {
    callbacks.onStatusChange('negotiating');
    try {
      await hub.invoke('RequestSession', folderId, shareToken ?? null);
    } catch {
      callbacks.onStatusChange('error');
      callbacks.onError('Не удалось запросить сессию у signaling-сервера');
    }
  }

  async function handleOffer(senderConnId: string, sdp: string): Promise<void> {
    const iceServers: RTCIceServer[] = [STUN];
    if (turnCredentials) {
      for (const uri of turnCredentials.uris) {
        iceServers.push({ urls: uri, username: turnCredentials.username, credential: turnCredentials.credential });
      }
    }

    pc = new RTCPeerConnection({ iceServers });

    pc.onicecandidate = async (e) => {
      if (!e.candidate) return;
      try {
        await hub.invoke('SendIceCandidate',
          senderConnId, e.candidate.candidate, e.candidate.sdpMid, e.candidate.sdpMLineIndex);
      } catch { /* non-fatal */ }
    };

    pc.ondatachannel = (e) => { attachDataChannel(e.channel); };

    await pc.setRemoteDescription({ type: 'offer', sdp });
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    await hub.invoke('SendAnswer', senderConnId, answer.sdp ?? '');
  }

  function handleIceCandidate(candidate: string, sdpMid: string | null, sdpMLineIndex: number | null): void {
    if (!pc) return;
    pc.addIceCandidate({
      candidate,
      sdpMid: sdpMid ?? undefined,
      sdpMLineIndex: sdpMLineIndex ?? undefined,
    }).catch(() => { /* ignore late candidates */ });
  }

  // ---- Data channel ----

  function attachDataChannel(channel: RTCDataChannel): void {
    dc = channel;
    dc.binaryType = 'arraybuffer';
    dc.onopen = () => void initiateEcdh();
    dc.onmessage = (e) => {
      if (typeof e.data === 'string') handleTextMessage(e.data);
      else void handleBinaryChunk(new Uint8Array(e.data as ArrayBuffer));
    };
    dc.onerror = () => {
      callbacks.onStatusChange('error');
      callbacks.onError('Data channel error');
    };
    // Channel may already be open when ondatachannel fires (race with ICE)
    if (dc.readyState === 'open') void initiateEcdh();
  }

  async function initiateEcdh(): Promise<void> {
    callbacks.onStatusChange('ecdh');
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
      hub.on('Offer', (senderConnId: string, sdp: string) => void handleOffer(senderConnId, sdp));
      hub.on('IceCandidate', (_: string, candidate: string, sdpMid: string | null, sdpMLineIndex: number | null) =>
        handleIceCandidate(candidate, sdpMid, sdpMLineIndex));
      hub.on('OwnerOnlineStatusChanged', () => { /* handled by useOwnerOnlineStatus */ });
      await hub.start();
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
