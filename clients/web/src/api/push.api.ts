import { api } from './client';

export const pushApi = {
  vapidKey: () =>
    api.get<{ publicKey: string }>('/api/notifications/push/vapid-public-key').then(r => r.data),
  subscribe: (sub: PushSubscriptionJSON) =>
    api.post('/api/notifications/push-subscriptions', sub),
  unsubscribe: (endpoint: string) =>
    api.delete('/api/notifications/push-subscriptions', { params: { endpoint } }),
};
