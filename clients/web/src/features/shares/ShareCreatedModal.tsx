import { useEffect } from 'react';
import { Icon } from '../../shared/Icon';

interface Props {
  open: boolean;
  token: string;
  onClose: () => void;
}

export function ShareCreatedModal({ open, token, onClose }: Props) {
  const shareUrl = `${window.location.origin}/s/${token}`;

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    if (open) document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  const copy = () => {
    navigator.clipboard.writeText(shareUrl).catch(() => {});
  };

  if (!open) return null;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()}>
        <div className="modal-head">
          <h3>Ссылка создана</h3>
        </div>
        <div className="modal-body">
          <div className="share-warn">
            <Icon name="shield" size={16} />
            <span>Сохраните ссылку — токен показывается только один раз.</span>
          </div>
          <div className="share-url-box">{shareUrl}</div>
          <button
            className="btn brand"
            style={{ width: '100%', justifyContent: 'center' }}
            onClick={copy}
          >
            <Icon name="copy" size={14} /> Копировать ссылку
          </button>
        </div>
        <div className="modal-foot">
          <button className="btn ghost" onClick={onClose}>Закрыть</button>
        </div>
      </div>
    </div>
  );
}
