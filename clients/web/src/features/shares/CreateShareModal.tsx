import { useEffect, useState } from 'react';
import { ShareCreatedModal } from './ShareCreatedModal';
import { useCreateShare } from './useShareMutations';
import type { CreateShareResponse } from '../../types/share';

interface Props {
  folderId: string;
  folderName?: string;
  open: boolean;
  onClose: () => void;
}

const DAY_OPTIONS = [
  { days: 1, label: '1 день' },
  { days: 7, label: 'Неделя' },
  { days: 30, label: 'Месяц' },
  { days: 90, label: '3 месяца' },
];

export function CreateShareModal({ folderId, folderName, open, onClose }: Props) {
  const [days, setDays] = useState(7);
  const [path, setPath] = useState('');
  const [created, setCreated] = useState<CreateShareResponse | null>(null);
  const create = useCreateShare(folderId);

  useEffect(() => {
    if (open) { setDays(7); setPath(''); }
  }, [open]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    if (open) document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  const handleCreate = async () => {
    const expiresAt = new Date(Date.now() + days * 24 * 3600 * 1000).toISOString();
    const res = await create.mutateAsync({ expiresAt, path: path.trim() || null });
    onClose();
    setCreated(res);
  };

  if (!open && !created) return null;

  return (
    <>
      {open && (
        <div className="modal-backdrop" onClick={onClose}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-head">
              <h3>Создать share-ссылку</h3>
              {folderName && <p>Папка «{folderName}». Любой с этой ссылкой получит доступ.</p>}
            </div>
            <div className="modal-body">
              <div className="field">
                <label>Срок действия</label>
                <div style={{ display: 'flex', gap: 6 }}>
                  {DAY_OPTIONS.map(opt => (
                    <button
                      key={opt.days}
                      type="button"
                      className={`btn sm${days === opt.days ? ' brand' : ''}`}
                      onClick={() => setDays(opt.days)}
                    >
                      {opt.label}
                    </button>
                  ))}
                </div>
              </div>
              <div className="field">
                <label>Открыть только конкретный файл (необязательно)</label>
                <input
                  value={path}
                  onChange={e => setPath(e.target.value)}
                  placeholder="например, Презентация.pdf"
                />
              </div>
            </div>
            <div className="modal-foot">
              <button className="btn ghost" onClick={onClose}>Отмена</button>
              <button className="btn brand" disabled={create.isPending} onClick={() => void handleCreate()}>
                {create.isPending ? 'Создание…' : <><svg width={13} height={13} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"><path d="M10 14a4 4 0 0 1 0-5.6l3-3a4 4 0 0 1 5.6 5.6l-1.5 1.5M14 10a4 4 0 0 1 0 5.6l-3 3a4 4 0 0 1-5.6-5.6l1.5-1.5"/></svg> Создать ссылку</>}
              </button>
            </div>
          </div>
        </div>
      )}
      {created && (
        <ShareCreatedModal
          open
          token={created.token}
          onClose={() => setCreated(null)}
        />
      )}
    </>
  );
}
