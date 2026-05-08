import { api } from './client';
import type { FolderDeviceResponse } from '../types/device';

export interface TurnCredentials {
  username: string;
  credential: string;
  uris: string[];
  ttl: number;
}

export const signalingApi = {
  getTurnCredentials: () =>
    api.get<TurnCredentials>('/api/signaling/turn/credentials').then(r => r.data),

  getFolderDevices: (folderIds: string[]) =>
    api.get<FolderDeviceResponse[]>('/api/signaling/devices/folder-devices', {
      params: new URLSearchParams(folderIds.map(id => ['folderIds', id])),
    }).then(r => r.data),

  claimFolder: (deviceId: string, folderId: string, force = false) =>
    api.post(`/api/signaling/devices/${deviceId}/folders/${folderId}`, null, {
      params: force ? { force: 'true' } : {},
    }),

  releaseFolder: (deviceId: string, folderId: string) =>
    api.delete(`/api/signaling/devices/${deviceId}/folders/${folderId}`),
};
