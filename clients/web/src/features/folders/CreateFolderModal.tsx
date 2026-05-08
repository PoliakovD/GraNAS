import { useEffect, useRef, useState } from 'react';
import { Icon } from '../../shared/Icon';
import { useCreateFolder } from './useFoldersQuery';

interface Props {
  open: boolean;
  parentFolderId?: string | null;
  parentName?: string | null;
  onClose: () => void;
}

export function CreateFolderModal({ open, parentFolderId, parentName, onClose }: Props) {
  const [name, setName] = useState('');
  const [error, setError] = useState('');
  const create = useCreateFolder();
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (open) {
      setName('');
      setError('');
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [open]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    if (open) document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  const handleCreate = async () => {
    if (!name.trim()) { setError('Введите название'); return; }
    await create.mutateAsync({ name: name.trim(), parentFolderId: parentFolderId ?? null });
    onClose();
  };

  if (!open) return null;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()}>
        <div className="modal-head">
          <h3>Новая папка</h3>
          <p>{parentName ? `Внутри: ${parentName}` : 'В корне ваших папок'}</p>
        </div>
        <div className="modal-body">
          <div className="field">
            <label>Имя папки</label>
            <input
              ref={inputRef}
              placeholder="Например, «Инвойсы 2026»"
              value={name}
              onChange={e => { setName(e.target.value); setError(''); }}
              onKeyDown={e => { if (e.key === 'Enter') void handleCreate(); }}
            />
            {error && <div className="field-error">{error}</div>}
          </div>
          <div style={{ background: 'var(--surface-2)', padding: 12, borderRadius: 10, display: 'flex', gap: 10, alignItems: 'flex-start' }}>
            <Icon name="shield" size={16} />
            <div style={{ fontSize: 12.5, color: 'var(--ink-700)', lineHeight: 1.4 }}>
              На сервер уйдут только имя и метаданные. Сами файлы добавите позже — они останутся на ваших устройствах.
            </div>
          </div>
        </div>
        <div className="modal-foot">
          <button className="btn ghost" onClick={onClose}>Отмена</button>
          <button className="btn brand" disabled={!name.trim() || create.isPending} onClick={() => void handleCreate()}>
            {create.isPending ? 'Создание…' : 'Создать'}
          </button>
        </div>
      </div>
    </div>
  );
}
