import { api } from './client';

export interface NotificationDto {
  id: string;
  type: string;
  data: Record<string, unknown>;
  isRead: boolean;
  createdAt: string;
}

export interface PagedNotificationsResponse {
  items: NotificationDto[];
  nextCursor: string | null;
}

export interface UnreadCountResponse {
  unreadCount: number;
}

export const listNotifications = (cursor?: string, take = 20) =>
  api.get<PagedNotificationsResponse>('/api/notifications', {
    params: { cursor, take },
  }).then(r => r.data);

export const getUnreadCount = () =>
  api.get<UnreadCountResponse>('/api/notifications/unread-count').then(r => r.data);

export const markRead = (id: string) =>
  api.post<void>(`/api/notifications/${id}/read`);

export const markAllRead = () =>
  api.post<void>('/api/notifications/read-all');
