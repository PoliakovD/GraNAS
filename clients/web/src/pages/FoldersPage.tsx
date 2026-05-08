import { useState } from 'react';
import { useNavigate, useOutletContext } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { FolderGrid } from '../features/folders/FolderGrid';
import { FolderListView } from '../features/folders/FolderListView';
import { CreateFolderModal } from '../features/folders/CreateFolderModal';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { useFolderDevices } from '../features/folders/useFolderDevices';
import { Icon } from '../shared/Icon';
import type { FolderResponse } from '../types/folder';

type View = 'grid' | 'list';
type OutletCtx = { openContext: (f: FolderResponse, x: number, y: number) => void };

export function FoldersPage() {
  const user = useCurrentUser();
  const navigate = useNavigate();
  const { data: folders = [], isLoading } = useFoldersQuery();
  const { openContext } = useOutletContext<OutletCtx>();

  const owned = folders.filter(f => f.ownerId === user.id && !f.parentFolderId);
  const { data: deviceMap } = useFolderDevices(owned.map(f => f.id));

  const [view, setView] = useState<View>('grid');
  const [selected, setSelected] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  const openFolder = (f: FolderResponse) => navigate(`/folders/${f.id}`);

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Мои папки</h1>
          <p className="page-sub">Папки, которыми вы владеете и которыми можете делиться</p>
        </div>
        <div className="head-actions">
          <button className="btn brand" onClick={() => setCreateOpen(true)}>
            <Icon name="plus" size={14} /> Новая папка
          </button>
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
          {owned.length} {owned.length === 1 ? 'элемент' : 'элементов'}
        </span>
      </div>

      {isLoading && (
        <div className="folder-grid">
          {[1, 2, 3, 4].map(i => (
            <div key={i} className="sk" style={{ height: 130, borderRadius: 14 }} />
          ))}
        </div>
      )}

      {!isLoading && owned.length === 0 && (
        <div className="empty">
          <div className="glyph"><Icon name="folder" size={32} /></div>
          <h4>Папок пока нет</h4>
          <p>Создайте первую папку — она появится здесь, и вы сможете выдать к ней доступ или отправить временную ссылку.</p>
          <button className="btn brand" onClick={() => setCreateOpen(true)}>
            <Icon name="plus" size={14} /> Создать первую папку
          </button>
        </div>
      )}

      {!isLoading && owned.length > 0 && view === 'grid' && (
        <FolderGrid
          folders={owned}
          currentUserId={user.id}
          deviceMap={deviceMap}
          selectedId={selected}
          onOpen={openFolder}
          onSelect={f => setSelected(f.id)}
          onContext={openContext}
        />
      )}

      {!isLoading && owned.length > 0 && view === 'list' && (
        <FolderListView
          folders={owned}
          currentUserId={user.id}
          onOpen={openFolder}
          onContext={openContext}
        />
      )}

      <CreateFolderModal
        open={createOpen}
        parentFolderId={null}
        onClose={() => setCreateOpen(false)}
      />
    </>
  );
}
