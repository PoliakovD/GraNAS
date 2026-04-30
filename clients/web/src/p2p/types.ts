import type { RemoteFileEntry } from './protocol';

export type { RemoteFileEntry };

export type OwnerStatus = 'online' | 'offline' | 'unknown';

export interface DownloadProgress {
  path: string;
  receivedBytes: number;
  totalBytes: number;
  percent: number;
}

export type SessionStatus =
  | 'idle'
  | 'connecting'
  | 'negotiating'
  | 'ecdh'
  | 'ready'
  | 'downloading'
  | 'error';
