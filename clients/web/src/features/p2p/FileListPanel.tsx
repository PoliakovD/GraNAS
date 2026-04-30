import { useState, useCallback, useRef } from 'react';
import { Button, Progress, Space, Table, Typography } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { signalingApi } from '../../api/signaling.api';
import { createP2PSession, type P2PSession } from '../../p2p/P2PSession';
import { OwnerStatusBadge } from './OwnerStatusBadge';
import { useOwnerOnlineStatus } from './useOwnerOnlineStatus';
import type { RemoteFileEntry, SessionStatus } from '../../p2p/types';

interface Props {
  folderId: string;
  shareToken?: string;
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
      onError: (msg) => console.error('[P2P]', msg),
    });

    sessionRef.current = session;
    await session.connect();
  }, [folderId, shareToken]);

  const downloadFile = useCallback((entry: RemoteFileEntry) => {
    sessionRef.current?.downloadFile(entry.path);
  }, []);

  const columns = [
    { title: 'Путь', dataIndex: 'path', key: 'path' },
    {
      title: 'Размер',
      dataIndex: 'size',
      key: 'size',
      render: (s: number) => `${(s / 1024).toFixed(1)} KB`,
      width: 100,
    },
    {
      title: '',
      key: 'action',
      width: 140,
      render: (_: unknown, entry: RemoteFileEntry) => {
        const pct = progress[entry.path];
        if (pct !== undefined) return <Progress percent={pct} size="small" style={{ width: 120 }} />;
        return (
          <Button
            size="small"
            icon={<DownloadOutlined />}
            onClick={() => void downloadFile(entry)}
            disabled={sessionStatus === 'downloading'}
          >
            Скачать
          </Button>
        );
      },
    },
  ];

  if (ownerStatus === 'offline') {
    return (
      <Space direction="vertical" style={{ width: '100%' }}>
        <OwnerStatusBadge status="offline" />
        <Typography.Text type="secondary">
          Владелец папки оффлайн. Файлы станут доступны, когда владелец откроет приложение.
        </Typography.Text>
      </Space>
    );
  }

  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Space>
        <OwnerStatusBadge status={ownerStatus} />
        {sessionStatus === 'idle' && ownerStatus === 'online' && (
          <Button type="primary" onClick={() => void connect()} size="small">
            Показать файлы
          </Button>
        )}
        {sessionStatus !== 'idle' && sessionStatus !== 'ready' && sessionStatus !== 'downloading' && (
          <Typography.Text type="secondary">Соединение…</Typography.Text>
        )}
      </Space>
      {files.length > 0 && (
        <Table
          dataSource={files}
          columns={columns}
          rowKey="path"
          size="small"
          pagination={false}
        />
      )}
    </Space>
  );
}
