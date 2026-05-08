import { Icon } from '../../shared/Icon';
import { initials, colorFromString, relTime } from '../../shared/format';
import type { FolderResponse } from '../../types/folder';

interface FolderListRowProps {
  folder: FolderResponse;
  ownerEmail: string | null;
  isOwner: boolean;
  onOpen: (f: FolderResponse) => void;
  onContext: (f: FolderResponse, x: number, y: number) => void;
}

function FolderListRow({ folder, ownerEmail, isOwner, onOpen, onContext }: FolderListRowProps) {
  return (
    <div
      className="list-row"
      onClick={() => onOpen(folder)}
      onContextMenu={e => { e.preventDefault(); onContext(folder, e.clientX, e.clientY); }}
    >
      <div className="name">
        <div className="icon-wrap">
          <Icon name={isOwner ? 'folder' : 'folder-shared'} size={15} />
        </div>
        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{folder.name}</span>
        {folder.accessLevel === 'View' && <span className="tag blue" style={{ marginLeft: 4 }}>чтение</span>}
        {folder.accessLevel === 'Full' && <span className="tag green" style={{ marginLeft: 4 }}>полный</span>}
      </div>
      <div className="meta-text">
        {ownerEmail ? (
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
            <span style={{
              width: 18, height: 18, fontSize: 10,
              display: 'inline-grid', placeItems: 'center',
              borderRadius: '50%',
              background: colorFromString(ownerEmail),
              color: '#fff',
              fontWeight: 600,
            }}>
              {initials(ownerEmail)}
            </span>
            {ownerEmail}
          </span>
        ) : 'Я'}
      </div>
      <div className="meta-text">{relTime(folder.updatedAt)}</div>
      <div className="meta-text">—</div>
      <button className="icon-btn" onClick={e => { e.stopPropagation(); onContext(folder, e.clientX, e.clientY); }}>
        <Icon name="more-v" size={14} />
      </button>
    </div>
  );
}

interface FolderListViewProps {
  folders: FolderResponse[];
  currentUserId: string;
  ownerEmailMap?: Record<string, string>;
  onOpen: (f: FolderResponse) => void;
  onContext: (f: FolderResponse, x: number, y: number) => void;
}

export function FolderListView({ folders, currentUserId, ownerEmailMap, onOpen, onContext }: FolderListViewProps) {
  return (
    <div className="list-table">
      <div className="list-row head">
        <span>Имя</span>
        <span>Владелец</span>
        <span>Изменено</span>
        <span>Элементов</span>
        <span></span>
      </div>
      {folders.map(f => (
        <FolderListRow
          key={f.id}
          folder={f}
          ownerEmail={ownerEmailMap?.[f.ownerId] ?? null}
          isOwner={f.ownerId === currentUserId}
          onOpen={onOpen}
          onContext={onContext}
        />
      ))}
    </div>
  );
}
