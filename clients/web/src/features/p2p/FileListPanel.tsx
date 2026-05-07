import { useCallback, useRef, useState } from 'react';
import { signalingApi } from '../../api/signaling.api';
import { createP2PSession, type P2PSession } from '../../p2p/P2PSession';
import { OwnerStatusBadge } from './OwnerStatusBadge';
import { useOwnerOnlineStatus } from './useOwnerOnlineStatus';
import type { RemoteFileEntry, SessionStatus } from '../../p2p/types';
import { Icon } from '../../shared/Icon';
import { fmtBytes, relTime } from '../../shared/format';

interface Props {
  folderId: string;
  shareToken?: string;
}

function extBadgeClass(path: string): string {
  const ext = path.split('.').pop()?.toLowerCase() ?? '';
  if (ext === 'pdf') return 'pdf';
  if (['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg'].includes(ext)) return 'img';
  if (ext === 'zip' || ext === 'rar' || ext === '7z' || ext === 'tar' || ext === 'gz') return 'zip';
  if (['doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx'].includes(ext)) return 'doc';
  if (['js', 'ts', 'tsx', 'jsx', 'py', 'rs', 'go', 'cs', 'java'].includes(ext)) return 'code';
  return 'misc';
}

export function FileListPanel({ folderId, shareToken }: Props) {
  const ownerStatus = useOwnerOnlineStatus(folderId);
  const [sessionStatus, setSessionStatus] = useState<SessionStatus>('idle');
  const [files, setFiles] = useState<RemoteFileEntry[]>([]);
  const [progress, setProgress] = useState<Record<string, number>>({});
  const sessionRef = useRef<P2PSession | null>(null);

  const connect = useCallback(async () => {
    let turnCredentials = null;
    try { turnCredentials = await signalingApi.getTurnCredentials(); } catch { /* optional */ }

    const session = createP2PSession(folderId, shareToken, turnCredentials, {
      onStatusChange: setSessionStatus,
      onFiles: setFiles,
      onDownloadProgress: (path, received, total) =>
        setProgress(p => ({ ...p, [path]: Math.round((received / total) * 100) })),
      onDownloadDone: (path, blob) => {
        setProgress(p => { const n = { ...p }; delete n[path]; return n; });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = path.split('/').pop() ?? path;
        a.click();
        URL.revokeObjectURL(url);
      },
      onError: msg => console.error('[P2P]', msg),
    });
    sessionRef.current = session;
    await session.connect();
  }, [folderId, shareToken]);

  const downloadFile = useCallback((entry: RemoteFileEntry) => {
    sessionRef.current?.downloadFile(entry.path);
  }, []);

  if (ownerStatus === 'offline') {
    return (
      <div className="empty">
        <div className="glyph" style={{ background: 'var(--warn-soft)', color: 'var(--warn)' }}>
          <Icon name="wifi" size={28} />
        </div>
        <h4>Владелец сейчас оффлайн</h4>
        <p>Файлы передаются напрямую между устройствами по WebRTC. Они станут доступны, как только владелец откроет приложение.</p>
      </div>
    );
  }

  const isConnecting = sessionStatus === 'connecting' || sessionStatus === 'negotiating' || sessionStatus === 'ecdh';

  if (sessionStatus === 'idle' || (files.length === 0 && !isConnecting && sessionStatus !== 'ready')) {
    return (
      <div className="empty">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 4 }}>
          <OwnerStatusBadge status={ownerStatus} />
          {isConnecting && (
            <span style={{ fontSize: 13, color: 'var(--ink-500)' }}>Соединение…</span>
          )}
        </div>
        {sessionStatus === 'idle' && ownerStatus === 'online' && (
          <>
            <div className="glyph">
              <Icon name="cloud" size={28} />
            </div>
            <h4>Запросить файлы у владельца</h4>
            <p>На сервере хранятся только метаданные. Список файлов придёт от владельца через защищённое P2P-соединение.</p>
            <button className="btn brand" onClick={() => void connect()}>
              <Icon name="wifi" size={14} /> Подключиться по P2P
            </button>
          </>
        )}
      </div>
    );
  }

  if (files.length === 0) {
    return (
      <div className="empty">
        <OwnerStatusBadge status={ownerStatus} />
        <h4>Файлов нет</h4>
        <p>Владелец не добавил файлы в эту папку.</p>
      </div>
    );
  }

  return (
    <div className="list-table">
      <div className="file-row head">
        <span></span>
        <span>Имя файла</span>
        <span>Изменён</span>
        <span>Прогресс</span>
        <span style={{ textAlign: 'right' }}>Размер</span>
        <span></span>
      </div>
      {files.map(f => {
        const pct = progress[f.path];
        const extCls = extBadgeClass(f.path);
        const ext = (f.path.split('.').pop() ?? '').toUpperCase().slice(0, 4);
        return (
          <div key={f.path} className="file-row">
            <span className={`ext-badge ${extCls}`}>{ext || '?'}</span>
            <div style={{ overflow: 'hidden' }}>
              <div style={{ fontWeight: 550, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{f.path}</div>
            </div>
            <span className="meta-text">{f.modifiedAt ? relTime(f.modifiedAt) : '—'}</span>
            <div>
              {pct != null ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <div className="progress-bar"><div style={{ width: `${pct}%` }} /></div>
                  <span style={{ fontSize: 11.5, color: 'var(--ink-500)', minWidth: 32 }}>{pct}%</span>
                </div>
              ) : (
                <span className="meta-text">—</span>
              )}
            </div>
            <span style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--ink-500)' }}>
              {fmtBytes(f.size)}
            </span>
            <button
              className="icon-btn"
              onClick={() => downloadFile(f)}
              disabled={sessionStatus === 'downloading'}
              title="Скачать"
            >
              <Icon name="download" size={15} />
            </button>
          </div>
        );
      })}
    </div>
  );
}
