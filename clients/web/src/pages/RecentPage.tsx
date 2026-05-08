import { useNavigate, useOutletContext } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { FolderGrid } from '../features/folders/FolderGrid';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { Icon } from '../shared/Icon';
import type { FolderResponse } from '../types/folder';

type OutletCtx = { openContext: (f: FolderResponse, x: number, y: number) => void };

export function RecentPage() {
  const user = useCurrentUser();
  const navigate = useNavigate();
  const { data: folders = [], isLoading } = useFoldersQuery();
  const { openContext } = useOutletContext<OutletCtx>();

  const recent = [...folders]
    .filter(f => f.lastAccessedAt ?? f.updatedAt)
    .sort((a, b) =>
      +new Date(b.lastAccessedAt ?? b.updatedAt ?? 0) -
      +new Date(a.lastAccessedAt ?? a.updatedAt ?? 0)
    )
    .slice(0, 12);

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Недавние</h1>
          <p className="page-sub">Папки, которые вы недавно открывали</p>
        </div>
      </div>

      {isLoading && (
        <div className="folder-grid">
          {[1, 2, 3, 4, 5, 6].map(i => <div key={i} className="sk" style={{ height: 130, borderRadius: 14 }} />)}
        </div>
      )}

      {!isLoading && recent.length === 0 && (
        <div className="empty">
          <div className="glyph"><Icon name="recent" size={28} /></div>
          <h4>Папок нет</h4>
          <p>Здесь появятся папки, которые вы недавно открывали или изменяли.</p>
        </div>
      )}

      {!isLoading && recent.length > 0 && (
        <FolderGrid
          folders={recent}
          currentUserId={user.id}
          onOpen={f => navigate(`/folders/${f.id}`)}
          onSelect={() => {}}
          onContext={openContext}
        />
      )}
    </>
  );
}
