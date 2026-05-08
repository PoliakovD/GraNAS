import { useNavigate } from 'react-router-dom';
import { useGlobalSharesQuery } from '../features/shares/useGlobalSharesQuery';
import { Icon } from '../shared/Icon';

export function LinksPage() {
  const navigate = useNavigate();
  const { data: shares, isLoading } = useGlobalSharesQuery();

  const copy = (url: string, e: React.MouseEvent) => {
    e.stopPropagation();
    navigator.clipboard.writeText(url).catch(() => {});
  };

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Share-ссылки</h1>
          <p className="page-sub">Активные временные ссылки на ваши папки и файлы</p>
        </div>
      </div>

      {isLoading && (
        <div className="list-table">
          {[1, 2, 3].map(i => (
            <div key={i} className="list-row" style={{ cursor: 'default', gridTemplateColumns: '1fr 200px 140px 100px 40px' }}>
              <div className="sk" style={{ height: 20, borderRadius: 4, width: '60%' }} />
              <div className="sk" style={{ height: 16, borderRadius: 4, width: 120 }} />
              <div className="sk" style={{ height: 16, borderRadius: 4, width: 80 }} />
              <div /><div />
            </div>
          ))}
        </div>
      )}

      {!isLoading && (!shares || shares.length === 0) && (
        <div className="empty">
          <div className="glyph" style={{ background: 'var(--info-soft)', color: 'var(--info)' }}>
            <Icon name="link" size={28} />
          </div>
          <h4>Ссылок пока нет</h4>
          <p>Откройте любую папку и создайте временную ссылку — удобный способ поделиться с теми, у кого нет аккаунта.</p>
        </div>
      )}

      {!isLoading && shares && shares.length > 0 && (
        <div className="list-table">
          <div className="list-row head" style={{ gridTemplateColumns: '1fr 200px 140px 100px 40px' }}>
            <span>Папка / файл</span>
            <span>Истекает</span>
            <span>Статус</span>
            <span>Открытий</span>
            <span></span>
          </div>
          {shares.map(s => (
            <div
              key={s.id}
              className="list-row"
              style={{ gridTemplateColumns: '1fr 200px 140px 100px 40px' }}
              onClick={() => navigate(`/folders/${s.folderId}`)}
            >
              <div className="name">
                <div className="icon-wrap"><Icon name="link" size={14} /></div>
                <div>
                  <div>{s.folderName}{s.path ? ` › ${s.path}` : ''}</div>
                  {s.shareUrl && (
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--ink-400)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: 320 }}>
                      {s.shareUrl}
                    </div>
                  )}
                </div>
              </div>
              <div className="meta-text">
                {s.expiresAt ? new Date(s.expiresAt).toLocaleDateString('ru-RU') : '—'}
              </div>
              <div>
                {s.revoked
                  ? <span className="tag red">отозвана</span>
                  : <span className="tag green"><span className="live-dot" />активна</span>}
              </div>
              <div className="meta-text">{s.openCount ?? '—'}</div>
              <button
                className="icon-btn"
                onClick={e => s.shareUrl ? copy(s.shareUrl, e) : e.stopPropagation()}
                disabled={!s.shareUrl}
                title={s.shareUrl ? 'Копировать ссылку' : 'URL недоступен'}
              >
                <Icon name="copy" size={14} />
              </button>
            </div>
          ))}
        </div>
      )}
    </>
  );
}
