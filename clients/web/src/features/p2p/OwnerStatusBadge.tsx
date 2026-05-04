import { Tooltip } from 'antd';
import type { OwnerStatus } from '../../p2p/types';

interface Props {
  status: OwnerStatus;
}

const CONFIG: Record<OwnerStatus, { color: string; label: string }> = {
  online:  { color: '#4caf50', label: 'Владелец онлайн — файлы доступны' },
  offline: { color: '#9e9e9e', label: 'Владелец оффлайн — файлы недоступны' },
  unknown: { color: '#d0d0d0', label: 'Статус владельца неизвестен' },
};

export function OwnerStatusBadge({ status }: Props) {
  const { color, label } = CONFIG[status];
  return (
    <Tooltip title={label}>
      <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
        <span
          style={{
            display: 'inline-block',
            width: 10,
            height: 10,
            borderRadius: '50%',
            background: color,
          }}
        />
        <span style={{ fontSize: 12, color: '#666' }}>
          {status === 'online' ? 'Online' : status === 'offline' ? 'Offline' : '…'}
        </span>
      </span>
    </Tooltip>
  );
}
