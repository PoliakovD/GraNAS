import { Typography } from 'antd';
import type { NotificationDto } from '../../api/notifications.api';

const LABELS: Record<string, (data: Record<string, unknown>) => string> = {
  'access.granted': d =>
    `${d.ownerName ?? 'Владелец'} предоставил доступ к «${d.folderName ?? 'папке'}»`,
  'access.revoked': d =>
    `Доступ к «${d.folderName ?? 'папке'}» отозван`,
  'share.revoked': d =>
    `Ссылка на «${d.folderName ?? 'папку'}» недействительна`,
  'access.lost': d =>
    `Папка «${d.folderName ?? 'папка'}» была удалена`,
};

interface Props {
  notification: NotificationDto;
}

export function NotificationItem({ notification }: Props) {
  const label =
    LABELS[notification.type]?.(notification.data) ??
    `Событие: ${notification.type}`;

  return (
    <Typography.Text style={{ opacity: notification.isRead ? 0.5 : 1 }}>
      {label}
    </Typography.Text>
  );
}
