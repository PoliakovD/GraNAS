export type NotificationType = 'access_granted' | 'access_revoked' | 'share_revoked' | 'access_lost';

export const ALL_NOTIFICATION_TYPES: NotificationType[] = [
  'access_granted',
  'access_revoked',
  'share_revoked',
  'access_lost',
];

export const NOTIFICATION_TYPE_LABELS: Record<NotificationType, string> = {
  access_granted: 'Доступ предоставлен',
  access_revoked: 'Доступ отозван',
  share_revoked:  'Ссылка отозвана',
  access_lost:    'Доступ потерян',
};
