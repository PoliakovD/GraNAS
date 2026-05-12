import type { NotificationType } from '../types/notification';
import { api } from './client';

export interface NotificationChannelPrefs {
  access_granted: boolean;
  access_revoked: boolean;
  share_revoked:  boolean;
  access_lost:    boolean;
}

export interface NotificationPrefs {
  email:   NotificationChannelPrefs;
  inApp:   NotificationChannelPrefs;
  webPush: NotificationChannelPrefs;
}

export interface UserSettingsResponse {
  notificationPrefs: NotificationPrefs;
}

export const defaultChannelPrefs = (value: boolean): NotificationChannelPrefs => ({
  access_granted: value,
  access_revoked: value,
  share_revoked:  value,
  access_lost:    value,
});

export const settingsApi = {
  getPrefs: () =>
    api.get<UserSettingsResponse>('/api/auth/me/settings').then(r => r.data),
  updatePrefs: (prefs: NotificationPrefs) =>
    api.put('/api/auth/me/settings', { notificationPrefs: prefs }),
};
