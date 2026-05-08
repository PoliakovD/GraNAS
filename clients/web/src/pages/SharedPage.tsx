import { useMemo, useState } from 'react';
import { useNavigate, useOutletContext } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { FolderGrid } from '../features/folders/FolderGrid';
import { FolderListView } from '../features/folders/FolderListView';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { Icon } from '../shared/Icon';
import { initials, colorFromString } from '../shared/format';
import type { FolderResponse } from '../types/folder';

type View = 'grid' | 'list';
type OutletCtx = { openContext: (f: FolderResponse, x: number, y: number) => void };

export function SharedPage() {
  const user = useCurrentUser();
  const navigate = useNavigate();
  const { data: folders = [], isLoading } = useFoldersQuery();
  const { openContext } = useOutletContext<OutletCtx>();
  const [view, setView] = useState<View>('list');

  const shared = folders.filter(f => f.ownerId !== user.id);

  const grouped = useMemo(() => {
    const map: Record<string, FolderResponse[]> = {};
    shared.forEach(f => {
      (map[f.ownerId] = map[f.ownerId] || []).push(f);
    });
    return map;
  }, [shared]);

  const openFolder = (f: FolderResponse) => navigate(`/folders/${f.id}`);

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Доступные папки</h1>
          <p className="page-sub">Папки, к которым вам открыли доступ другие пользователи</p>
        </div>
      </div>

      <div className="toolbar">
        <div className="seg">
          <button className={view === 'grid' ? 'on' : ''} onClick={() => setView('grid')}>
            <Icon name="grid" size={13} /> Сетка
          </button>
          <button className={view === 'list' ? 'on' : ''} onClick={() => setView('list')}>
            <Icon name="list" size={13} /> Список
          </button>
        </div>
        <span style={{ fontSize: 12.5, color: 'var(--ink-500)', marginLeft: 8 }}>
          {shared.length} {shared.length === 1 ? 'элемент' : 'элементов'}
        </span>
      </div>

      {isLoading && (
        <div className="folder-grid">
          {[1, 2, 3].map(i => <div key={i} className="sk" style={{ height: 130, borderRadius: 14 }} />)}
        </div>
      )}

      {!isLoading && shared.length === 0 && (
        <div className="empty">
          <div className="glyph"><Icon name="shared" size={28} /></div>
          <h4>Никто не открыл вам доступ</h4>
          <p>Когда коллеги выдадут вам доступ к своим папкам, они появятся в этом списке. Получите уведомление прямо здесь.</p>
        </div>
      )}

      {!isLoading && Object.entries(grouped).map(([ownerId, items]) => {
        const ownerLabel = items[0]?.ownerEmail ?? (ownerId.slice(0, 8) + '…');
        return (
          <div key={ownerId} style={{ marginBottom: 22 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
              <div style={{
                width: 26, height: 26, borderRadius: '50%',
                background: colorFromString(ownerId),
                color: '#fff',
                display: 'grid', placeItems: 'center',
                fontWeight: 600, fontSize: 11,
              }}>
                {initials(items[0]?.ownerEmail ?? ownerId)}
              </div>
              <div style={{ fontWeight: 600, fontSize: 13.5 }}>
                {ownerLabel}
              </div>
              <span className="tag">{items.length} {items.length === 1 ? 'папка' : 'папки'}</span>
            </div>
            {view === 'grid' ? (
              <FolderGrid
                folders={items}
                currentUserId={user.id}
                onOpen={openFolder}
                onSelect={() => {}}
                onContext={openContext}
              />
            ) : (
              <FolderListView
                folders={items}
                currentUserId={user.id}
                onOpen={openFolder}
                onContext={openContext}
              />
            )}
          </div>
        );
      })}
    </>
  );
}
