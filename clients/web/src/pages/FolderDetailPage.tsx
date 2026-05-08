import { useEffect, useState } from 'react';
import { useNavigate, useOutletContext, useParams } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { FolderGrid } from '../features/folders/FolderGrid';
import { FolderListView } from '../features/folders/FolderListView';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { useTouchFolder } from '../features/folders/useTouchFolder';
import { FileListPanel } from '../features/p2p/FileListPanel';
import { OwnerStatusBadge } from '../features/p2p/OwnerStatusBadge';
import { useOwnerOnlineStatus } from '../features/p2p/useOwnerOnlineStatus';
import { Icon } from '../shared/Icon';
import { relTime } from '../shared/format';
import type { FolderResponse } from '../types/folder';

type View = 'grid' | 'list';
type OutletCtx = { openContext: (f: FolderResponse, x: number, y: number) => void };

export function FolderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const user = useCurrentUser();
  const navigate = useNavigate();
  const { data: folders = [], isLoading } = useFoldersQuery();
  const { openContext } = useOutletContext<OutletCtx>();
  const ownerStatus = useOwnerOnlineStatus(id);
  const [subView, setSubView] = useState<View>('grid');
  const touch = useTouchFolder();

  useEffect(() => {
    if (id) touch(id);
  }, [id]);

  if (!id) return null;

  const folder = folders.find(f => f.id === id);

  if (!isLoading && !folder) {
    return (
      <div className="empty">
        <div className="glyph" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }}>
          <Icon name="folder" size={32} />
        </div>
        <h4>Папка не найдена</h4>
        <p>Папка не существует или у вас нет доступа.</p>
        <button className="btn brand" onClick={() => navigate('/folders')}>
          К моим папкам
        </button>
      </div>
    );
  }

  if (isLoading || !folder) {
    return (
      <div>
        <div className="page-head">
          <div className="sk" style={{ width: 44, height: 44, borderRadius: 12, flexShrink: 0 }} />
          <div style={{ flex: 1 }}>
            <div className="sk" style={{ height: 24, width: 200, borderRadius: 6, marginBottom: 8 }} />
            <div className="sk" style={{ height: 16, width: 300, borderRadius: 4 }} />
          </div>
        </div>
      </div>
    );
  }

  const isOwner = folder.ownerId === user.id;
  const subfolders = folders.filter(f => f.parentFolderId === id);

  return (
    <>
      <div className="page-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 14, minWidth: 0, flex: 1 }}>
          <div style={{
            width: 44, height: 44, borderRadius: 12,
            display: 'grid', placeItems: 'center',
            background: 'var(--brand-primary-soft)',
            color: 'var(--brand-primary)',
            flexShrink: 0,
          }}>
            <Icon name="folder" size={22} />
          </div>
          <div style={{ minWidth: 0 }}>
            <h1 className="page-title" style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
              {folder.name}
              {!isOwner && folder.accessLevel === 'View' && <span className="tag blue">только чтение</span>}
              {!isOwner && folder.accessLevel === 'Full' && <span className="tag green">полный доступ</span>}
            </h1>
            <p className="page-sub">
              {isOwner ? 'Владелец: вы' : `Владелец: ${folder.ownerEmail ?? (folder.ownerId.slice(0, 8) + '…')}`}
              {' · '}{subfolders.length} подпапок
              {' · '}обновлено {relTime(folder.updatedAt)}
            </p>
          </div>
        </div>
        <div className="head-actions">
          <OwnerStatusBadge status={ownerStatus} />
        </div>
      </div>

      {subfolders.length > 0 && (
        <div style={{ marginBottom: 28 }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
            <div className="section-title" style={{ margin: 0 }}>Подпапки</div>
            <div className="seg">
              <button className={subView === 'grid' ? 'on' : ''} onClick={() => setSubView('grid')}>
                <Icon name="grid" size={12} />
              </button>
              <button className={subView === 'list' ? 'on' : ''} onClick={() => setSubView('list')}>
                <Icon name="list" size={12} />
              </button>
            </div>
          </div>
          {subView === 'grid' ? (
            <FolderGrid
              folders={subfolders}
              currentUserId={user.id}
              onOpen={f => navigate(`/folders/${f.id}`)}
              onSelect={() => {}}
              onContext={openContext}
            />
          ) : (
            <FolderListView
              folders={subfolders}
              currentUserId={user.id}
              onOpen={f => navigate(`/folders/${f.id}`)}
              onContext={openContext}
            />
          )}
        </div>
      )}

      <div style={{ marginBottom: 12, display: 'flex', alignItems: 'center', gap: 8 }}>
        <div className="section-title" style={{ margin: 0 }}>Файлы</div>
        {ownerStatus !== 'unknown' && (
          <span style={{ fontSize: 12, color: 'var(--ink-400)' }}>
            · P2P, только от владельца в реальном времени
          </span>
        )}
      </div>
      <FileListPanel folderId={id} />
    </>
  );
}
