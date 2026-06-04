import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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

// ── Tree types & builder ──────────────────────────────────────────────────────

type FileNode =
  | { kind: 'file'; name: string; entry: RemoteFileEntry }
  | { kind: 'dir'; name: string; path: string; children: FileNode[] };

function buildTree(entries: RemoteFileEntry[]): FileNode[] {
  const roots: FileNode[] = [];
  const dirMap = new Map<string, FileNode & { kind: 'dir' }>();

  function getOrCreateDir(path: string): FileNode & { kind: 'dir' } {
    if (dirMap.has(path)) return dirMap.get(path)!;
    const parts = path.split('/');
    const dir: FileNode & { kind: 'dir' } = { kind: 'dir', name: parts[parts.length - 1], path, children: [] };
    dirMap.set(path, dir);
    const parentPath = parts.slice(0, -1).join('/');
    if (parentPath === '') roots.push(dir);
    else getOrCreateDir(parentPath).children.push(dir);
    return dir;
  }

  for (const entry of entries) {
    const parts = entry.path.split('/');
    const name = parts[parts.length - 1];
    const fileNode: FileNode = { kind: 'file', name, entry };
    if (parts.length === 1) roots.push(fileNode);
    else getOrCreateDir(parts.slice(0, -1).join('/')).children.push(fileNode);
  }

  function sortLevel(nodes: FileNode[]) {
    nodes.sort((a, b) => {
      if (a.kind !== b.kind) return a.kind === 'dir' ? -1 : 1;
      return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
    });
    for (const n of nodes) if (n.kind === 'dir') sortLevel(n.children);
  }
  sortLevel(roots);
  return roots;
}

function extBadgeClass(path: string): string {
  const ext = path.split('.').pop()?.toLowerCase() ?? '';
  if (ext === 'pdf') return 'pdf';
  if (['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg'].includes(ext)) return 'img';
  if (['zip', 'rar', '7z', 'tar', 'gz'].includes(ext)) return 'zip';
  if (['doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx'].includes(ext)) return 'doc';
  if (['js', 'ts', 'tsx', 'jsx', 'py', 'rs', 'go', 'cs', 'java'].includes(ext)) return 'code';
  return 'misc';
}

// ── Tree renderer ─────────────────────────────────────────────────────────────

interface TreeRowsProps {
  nodes: FileNode[];
  depth: number;
  expanded: Set<string>;
  toggle: (path: string) => void;
  progress: Record<string, number>;
  status: SessionStatus;
  onDownload: (entry: RemoteFileEntry) => void;
}

function TreeRows({ nodes, depth, expanded, toggle, progress, status, onDownload }: TreeRowsProps) {
  return (
    <>
      {nodes.map(node => {
        if (node.kind === 'dir') {
          const isOpen = expanded.has(node.path);
          return (
            <div key={node.path}>
              <div
                className="tree-dir-row"
                style={{ paddingLeft: depth * 16 + 10 }}
                onClick={() => toggle(node.path)}
                role="button"
                aria-expanded={isOpen}
              >
                <Icon
                  name="chevron-right"
                  size={13}
                  className={`tree-chevron${isOpen ? ' open' : ''}`}
                />
                <span style={{ color: 'var(--warn)', lineHeight: 0, flexShrink: 0 }}>
                  <Icon name="folder" size={15} />
                </span>
                <span className="tree-dir-name">{node.name}</span>
                <span className="meta-text" style={{ marginLeft: 'auto', fontSize: 11 }}>
                  {node.children.length}
                </span>
              </div>
              {isOpen && (
                <TreeRows
                  nodes={node.children}
                  depth={depth + 1}
                  expanded={expanded}
                  toggle={toggle}
                  progress={progress}
                  status={status}
                  onDownload={onDownload}
                />
              )}
            </div>
          );
        }

        const ext = (node.entry.path.split('.').pop() ?? '').toUpperCase().slice(0, 4);
        const extCls = extBadgeClass(node.entry.path);
        const pct = progress[node.entry.path];
        return (
          <div key={node.entry.path} className="file-row" style={{ paddingLeft: depth * 16 + 14 }}>
            <span className={`ext-badge ${extCls}`}>{ext || '?'}</span>
            <div style={{ overflow: 'hidden' }}>
              <div style={{ fontWeight: 550, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {node.name}
              </div>
            </div>
            <span className="meta-text">{node.entry.modifiedAt ? relTime(node.entry.modifiedAt) : '—'}</span>
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
              {fmtBytes(node.entry.size)}
            </span>
            <button
              className="icon-btn"
              onClick={() => onDownload(node.entry)}
              disabled={status === 'downloading'}
              title="Скачать"
            >
              <Icon name="download" size={15} />
            </button>
          </div>
        );
      })}
    </>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export function FileListPanel({ folderId, shareToken }: Props) {
  const ownerStatus = useOwnerOnlineStatus(folderId);
  const [sessionStatus, setSessionStatus] = useState<SessionStatus>('idle');
  const [files, setFiles] = useState<RemoteFileEntry[]>([]);
  const [progress, setProgress] = useState<Record<string, number>>({});
  const sessionRef = useRef<P2PSession | null>(null);

  const tree = useMemo(() => buildTree(files), [files]);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const initExpandedRef = useRef(false);

  // Auto-expand all dirs when tree first loads
  useEffect(() => {
    if (tree.length === 0 || initExpandedRef.current) return;
    initExpandedRef.current = true;
    const allDirs = new Set<string>();
    function collect(nodes: FileNode[]) {
      for (const n of nodes) if (n.kind === 'dir') { allDirs.add(n.path); collect(n.children); }
    }
    collect(tree);
    setExpanded(allDirs);
  }, [tree]);

  const toggle = useCallback((path: string) => {
    setExpanded(prev => {
      const next = new Set(prev);
      if (next.has(path)) next.delete(path); else next.add(path);
      return next;
    });
  }, []);

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

  if (sessionStatus === 'idle' || (tree.length === 0 && !isConnecting && sessionStatus !== 'ready')) {
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

  if (tree.length === 0) {
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
      <TreeRows
        nodes={tree}
        depth={0}
        expanded={expanded}
        toggle={toggle}
        progress={progress}
        status={sessionStatus}
        onDownload={downloadFile}
      />
    </div>
  );
}
