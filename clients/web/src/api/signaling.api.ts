import { api } from './client';

export interface TurnCredentials {
  username: string;
  credential: string;
  uris: string[];
  ttl: number;
}

export const signalingApi = {
  getTurnCredentials: () =>
    api.get<TurnCredentials>('/api/signaling/turn/credentials').then(r => r.data),
};
