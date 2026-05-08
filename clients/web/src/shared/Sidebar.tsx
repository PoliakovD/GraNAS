import { useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { Icon } from './Icon';
import type { FolderResponse } from '../types/folder';

interface TreeNodeProps {
  folder: FolderResponse;
  all: FolderResponse[];
  depth: number;
  currentId: string | undefined;
  onSelect: (id: string) => void;
  expanded: Set<string>;
  setExpanded: React.Dispatch<React.SetStateAction<Set<string>>>;
}

function TreeNode({ folder, all, depth, currentId, onSelect, expanded, setExpanded }: TreeNodeProps) {
  const children = all.filter(f => f.parentFolderId === folder.id);
  const isOpen = expanded.has(folder.id);
  const hasChildren = children.length > 0;

  return (
    <div>
      <button
        className={`tree-node${currentId === folder.id ? ' active' : ''}`}
        style={{ paddingLeft: depth * 8 + 4 }}
        onClick={() => onSelect(folder.id)}
      >
        <span
          className={`tree-toggle${hasChildren ? '' : ' placeholder'}`}
          style={{ transform: isOpen ? 'rotate(90deg)' : 'rotate(0)' }}
          onClick={e => {
            e.stopPropagation();
            if (!hasChildren) return;
            setExpanded(prev => {
              const next = new Set(prev);
              isOpen ? next.delete(folder.id) : next.add(folder.id);
              return next;
            });
          }}
        >
          <Icon name="chevron-right" size={12} />
        </span>
        <Icon name="folder" size={14} />
        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', flex: 1 }}>
          {folder.name}
        </span>
      </button>
      {isOpen && hasChildren && (
        <div className="tree-children">
          {children.map(c => (
            <TreeNode
              key={c.id}
              folder={c}
              all={all}
              depth={depth + 1}
              currentId={currentId}
              onSelect={onSelect}
              expanded={expanded}
              setExpanded={setExpanded}
            />
          ))}
        </div>
      )}
    </div>
  );
}

interface SidebarProps {
  onCreateFolder: () => void;
}

export function Sidebar({ onCreateFolder }: SidebarProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const user = useCurrentUser();
  const { data: folders = [] } = useFoldersQuery();
  const { id: currentFolderId } = useParams<{ id: string }>();
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const owned = folders.filter(f => f.ownerId === user.id && !f.parentFolderId);
  const sharedCount = folders.filter(f => f.ownerId !== user.id).length;

  const active = (path: string) => location.pathname === path || location.pathname.startsWith(path + '/');

  const navItem = (path: string, icon: Parameters<typeof Icon>[0]['name'], label: string, count?: number) => (
    <button
      className={`nav-item${active(path) ? ' active' : ''}`}
      onClick={() => navigate(path)}
    >
      <Icon name={icon} size={16} />
      {label}
      {count !== undefined && <span className="nav-count">{count}</span>}
    </button>
  );

  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark">G</div>
        <div className="brand-name">GraNAS</div>
      </div>

      <button className="search-pill">
        <Icon name="search" size={14} />
        <span>Поиск папок и файлов…</span>
        <span className="kbd">⌘K</span>
      </button>

      <div className="nav-section">
        {navItem('/', 'home', 'Главная')}
        {navItem('/folders', 'folder', 'Мои папки', owned.length)}
        {navItem('/shared', 'shared', 'Доступные', sharedCount || undefined)}
        {navItem('/links', 'link', 'Ссылки')}
        {navItem('/recent', 'recent', 'Недавние')}
      </div>

      <div className="nav-section">
        <div className="nav-label">Дерево папок</div>
        <div className="tree">
          {owned.map(f => (
            <TreeNode
              key={f.id}
              folder={f}
              all={folders}
              depth={0}
              currentId={currentFolderId}
              onSelect={id => navigate(`/folders/${id}`)}
              expanded={expanded}
              setExpanded={setExpanded}
            />
          ))}
          <button className="tree-node" style={{ color: 'var(--ink-400)' }} onClick={onCreateFolder}>
            <span className="tree-toggle placeholder"><Icon name="chevron-right" size={12} /></span>
            <Icon name="plus" size={14} />
            <span>Новая папка</span>
          </button>
        </div>
      </div>

      <div className="storage-card">
        <div className="top">
          <span>Метаданные</span>
          <span><b>{folders.length} папок</b></span>
        </div>
        <div className="storage-bar"><div className="storage-fill" style={{ width: `${Math.min(folders.length * 2, 100)}%` }} /></div>
        <div className="hint">Файлы остаются на ваших устройствах. На сервере — только метаданные и права доступа.</div>
      </div>
    </aside>
  );
}
