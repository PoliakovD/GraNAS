import { Icon } from '../../shared/Icon';
import { initials, colorFromString, relTime } from '../../shared/format';
import type { FolderResponse } from '../../types/folder';
import type { ContextMenuItem } from '../../shared/ContextMenu';

interface FolderCardProps {
  folder: FolderResponse;
  selected: boolean;
  ownerEmail: string | null;
  isOwner: boolean;
  onOpen: (f: FolderResponse) => void;
  onSelect: (f: FolderResponse) => void;
  onContext: (f: FolderResponse, x: number, y: number) => void;
}

function FolderCard({ folder, selected, ownerEmail, isOwner, onOpen, onSelect, onContext }: FolderCardProps) {
  const iconCls = !isOwner ? 'shared' : '';

  return (
    <div
      className={`folder-card${selected ? ' selected' : ''}`}
      onClick={e => { if (e.detail === 2) onOpen(folder); else onSelect(folder); }}
      onContextMenu={e => { e.preventDefault(); onContext(folder, e.clientX, e.clientY); }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div className={`icon-wrap ${iconCls}`}>
          <Icon name={isOwner ? 'folder' : 'folder-shared'} size={18} />
        </div>
        {folder.accessLevel === 'View' && <span className="tag blue">только чтение</span>}
        {folder.accessLevel === 'Full' && <span className="tag green">полный доступ</span>}
      </div>

      <div className="card-actions">
        <button className="icon-btn" onClick={e => { e.stopPropagation(); onContext(folder, e.clientX, e.clientY); }}>
          <Icon name="more-v" size={14} />
        </button>
      </div>

      <div>
        <div className="name">{folder.name}</div>
        <div className="meta">
          <span>обновлено {relTime(folder.updatedAt)}</span>
        </div>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 'auto' }}>
        {ownerEmail ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 11.5, color: 'var(--ink-500)' }}>
            <span
              style={{
                width: 18, height: 18, fontSize: 10,
                display: 'inline-grid', placeItems: 'center',
                borderRadius: '50%',
                background: colorFromString(ownerEmail),
                color: '#fff',
              }}
            >
              {initials(ownerEmail)}
            </span>
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: 140 }}>
              {ownerEmail}
            </span>
          </div>
        ) : (
          <span className="tag"><Icon name="lock" size={11} /> приватная</span>
        )}
      </div>
    </div>
  );
}

interface FolderGridProps {
  folders: FolderResponse[];
  currentUserId: string;
  ownerEmailMap?: Record<string, string>;
  selectedId?: string | null;
  onOpen: (f: FolderResponse) => void;
  onSelect: (f: FolderResponse) => void;
  onContext: (f: FolderResponse, x: number, y: number) => void;
}

export function FolderGrid({ folders, currentUserId, ownerEmailMap, selectedId, onOpen, onSelect, onContext }: FolderGridProps) {
  return (
    <div className="folder-grid">
      {folders.map(f => (
        <FolderCard
          key={f.id}
          folder={f}
          selected={selectedId === f.id}
          ownerEmail={ownerEmailMap?.[f.ownerId] ?? null}
          isOwner={f.ownerId === currentUserId}
          onOpen={onOpen}
          onSelect={onSelect}
          onContext={onContext}
        />
      ))}
    </div>
  );
}

// Re-export ContextMenuItem for convenience
export type { ContextMenuItem };
