import { Icon } from '../../shared/Icon';
import type { OwnerStatus } from '../../p2p/types';

export function OwnerStatusBadge({ status }: { status: OwnerStatus }) {
  if (status === 'online') {
    return (
      <span className="tag green">
        <span className="live-dot" />
        онлайн
      </span>
    );
  }
  if (status === 'offline') {
    return (
      <span className="tag" style={{ color: 'var(--ink-500)' }}>
        <Icon name="wifi" size={11} />
        оффлайн
      </span>
    );
  }
  return (
    <span className="tag" style={{ color: 'var(--ink-400)' }}>
      статус неизвестен
    </span>
  );
}
