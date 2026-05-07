import { useState } from 'react';
import { Outlet, useMatch, useNavigate } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { Inspector } from '../features/inspector/Inspector';
import { CreateFolderModal } from '../features/folders/CreateFolderModal';
import { CreateShareModal } from '../features/shares/CreateShareModal';
import { ContextMenu, type ContextMenuItem } from './ContextMenu';
import { ToastContainer } from './Toast';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';
import { useDeleteFolder } from '../features/folders/useFoldersQuery';
import type { FolderResponse } from '../types/folder';

interface CtxState {
  x: number;
  y: number;
  items: ContextMenuItem[];
}

export function AppLayout() {
  const user = useCurrentUser();
  const { data: folders = [] } = useFoldersQuery();
  const match = useMatch('/folders/:id');
  const currentFolderId = match?.params.id;
  const currentFolder = currentFolderId ? folders.find(f => f.id === currentFolderId) : null;
  const isOwner = currentFolder ? currentFolder.ownerId === user.id : false;

  const [createOpen, setCreateOpen] = useState(false);
  const [createParent, setCreateParent] = useState<string | null>(null);
  const [shareOpen, setShareOpen] = useState(false);
  const [ctx, setCtx] = useState<CtxState | null>(null);

  const navigate = useNavigate();
  const deleteFolder = useDeleteFolder();

  const openContext = (folder: FolderResponse, x: number, y: number) => {
    const items: ContextMenuItem[] = [
      { icon: 'folder', label: 'Открыть', onClick: () => navigate(`/folders/${folder.id}`), kbd: '↵' },
      { sep: true },
      { icon: 'plus', label: 'Создать подпапку', onClick: () => {
        setCreateParent(folder.id);
        setCreateOpen(true);
      } },
      { icon: 'link', label: 'Поделиться по ссылке', onClick: () => navigate(`/folders/${folder.id}`) },
      { sep: true },
      { icon: 'trash', label: 'Удалить', danger: true, onClick: () => {
        if (confirm(`Удалить папку «${folder.name}»?`)) {
          deleteFolder.mutate(folder.id);
        }
      } },
    ];
    setCtx({ x, y, items });
  };

  const inspectorVisible = !!currentFolder;

  return (
    <div className={`app-shell${inspectorVisible ? ' with-inspector' : ''}`}>
      <Sidebar onCreateFolder={() => { setCreateParent(null); setCreateOpen(true); }} />

      <div className="main">
        <Topbar />
        <div className="content">
          <Outlet context={{ openContext }} />
        </div>
      </div>

      {inspectorVisible && currentFolder && (
        <Inspector
          folder={currentFolder}
          isOwner={isOwner}
          onCreateShare={() => setShareOpen(true)}
        />
      )}

      <CreateFolderModal
        open={createOpen}
        parentFolderId={createParent}
        parentName={createParent ? folders.find(f => f.id === createParent)?.name : null}
        onClose={() => { setCreateOpen(false); setCreateParent(null); }}
      />

      {currentFolder && (
        <CreateShareModal
          folderId={currentFolder.id}
          folderName={currentFolder.name}
          open={shareOpen}
          onClose={() => setShareOpen(false)}
        />
      )}

      {ctx && <ContextMenu x={ctx.x} y={ctx.y} items={ctx.items} onClose={() => setCtx(null)} />}
      <ToastContainer />
    </div>
  );
}

export type { CtxState };
