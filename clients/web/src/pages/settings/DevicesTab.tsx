import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { signalingApi } from '../../api/signaling.api';
import { relTime } from '../../shared/format';
import { toast } from '../../shared/useToast';
import type { DeviceResponse, DeviceFolderBinding } from '../../types/device';
import type { FolderResponse } from '../../types/folder';
import { FOLDERS_KEY } from '../../features/folders/useFoldersQuery';

const DEVICES_KEY = ['devices'] as const;
const DEVICE_FOLDERS_KEY = (id: string) => ['device-folders', id] as const;

function platformIcon(platform: DeviceResponse['platform']) {
  const icons: Record<string, string> = {
    Windows: '🖥',
    Linux: '🐧',
    MacOS: '🍎',
    Web: '🌐',
  };
  return icons[platform] ?? '💻';
}

function InlineRename({ device }: { device: DeviceResponse }) {
  const qc = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(device.deviceName);

  const rename = useMutation({
    mutationFn: (name: string) => signalingApi.renameDevice(device.deviceId, name),
    onSuccess: updated => {
      qc.setQueryData<DeviceResponse[]>(DEVICES_KEY, prev =>
        prev?.map(d => d.deviceId === updated.deviceId ? updated : d) ?? []);
      setEditing(false);
      toast('Устройство переименовано');
    },
    onError: (err: unknown) => {
      const status = (err as { response?: { status?: number } }).response?.status;
      if (status === 409) toast('Имя уже используется другим устройством');
      else toast('Не удалось переименовать устройство');
      setValue(device.deviceName);
      setEditing(false);
    },
  });

  if (editing) {
    return (
      <input
        className="device-name-input"
        value={value}
        autoFocus
        onChange={e => setValue(e.target.value)}
        onKeyDown={e => {
          if (e.key === 'Enter' && value.trim()) rename.mutate(value.trim());
          if (e.key === 'Escape') { setValue(device.deviceName); setEditing(false); }
        }}
        onBlur={() => { setValue(device.deviceName); setEditing(false); }}
        maxLength={100}
      />
    );
  }

  return (
    <span
      className="device-name-editable"
      title="Нажмите для переименования"
      onClick={() => setEditing(true)}
    >
      {device.deviceName}
    </span>
  );
}

function DeviceFoldersPanel({ deviceId }: { deviceId: string }) {
  const qc = useQueryClient();
  const { data: bindings = [], isLoading } = useQuery({
    queryKey: DEVICE_FOLDERS_KEY(deviceId),
    queryFn: () => signalingApi.getDeviceFolders(deviceId),
    staleTime: 30_000,
  });
  const folders = qc.getQueryData<FolderResponse[]>(FOLDERS_KEY) ?? [];
  const folderName = (id: string) => folders.find(f => f.id === id)?.name ?? id.slice(0, 8) + '…';

  const release = useMutation({
    mutationFn: ({ folderId }: { folderId: string }) =>
      signalingApi.releaseFolder(deviceId, folderId),
    onSuccess: (_, { folderId }) => {
      qc.setQueryData<DeviceFolderBinding[]>(DEVICE_FOLDERS_KEY(deviceId), prev =>
        prev?.filter(b => b.folderId !== folderId) ?? []);
      qc.invalidateQueries({ queryKey: ['folder-devices'] });
      toast('Папка отвязана');
    },
    onError: () => toast('Не удалось отвязать папку'),
  });

  if (isLoading) return <div className="empty-state" style={{ padding: '8px 0' }}><div className="spinner" /></div>;
  if (!bindings.length) return <p style={{ color: 'var(--ink-500)', fontSize: 13, margin: '6px 0' }}>Нет привязанных папок.</p>;

  return (
    <ul className="device-folders-list">
      {bindings.map(b => (
        <li key={b.folderId} className="device-folders-item">
          <span className="device-folders-name">{folderName(b.folderId)}</span>
          <span className="device-folders-claimed">{relTime(b.claimedAt)}</span>
          <button
            className="btn ghost sm"
            onClick={() => release.mutate({ folderId: b.folderId })}
            disabled={release.isPending}
          >
            Отвязать
          </button>
        </li>
      ))}
    </ul>
  );
}

function DeviceRow({ device }: { device: DeviceResponse }) {
  const qc = useQueryClient();
  const [expanded, setExpanded] = useState(false);

  const terminate = useMutation({
    mutationFn: () => signalingApi.terminateSession(device.deviceId),
    onSuccess: () => {
      qc.setQueryData<DeviceResponse[]>(DEVICES_KEY, prev =>
        prev?.map(d => d.deviceId === device.deviceId ? { ...d, isOnline: false } : d) ?? []);
      toast('Сессия завершена');
    },
    onError: () => toast('Не удалось завершить сессию'),
  });

  return (
    <div className={`device-row${expanded ? ' expanded' : ''}`}>
      <div className="device-row-main">
        <button
          className="device-expand-btn"
          onClick={() => setExpanded(v => !v)}
          aria-expanded={expanded}
        >
          {expanded ? '▾' : '▸'}
        </button>
        <span className="device-platform-icon">{platformIcon(device.platform)}</span>
        <InlineRename device={device} />
        <span className={`device-status${device.isOnline ? ' online' : ''}`}>
          {device.isOnline
            ? <><span className="live-dot green" />онлайн</>
            : relTime(device.lastSeenAt)}
        </span>
        <div className="device-actions">
          <button
            className="btn ghost sm"
            onClick={() => terminate.mutate()}
            disabled={!device.isOnline || terminate.isPending}
            title="Принудительно завершить сессию"
          >
            Отключить
          </button>
        </div>
      </div>
      {expanded && (
        <div className="device-folders-panel">
          <DeviceFoldersPanel deviceId={device.deviceId} />
        </div>
      )}
    </div>
  );
}

export function DevicesTab() {
  const { data: devices, isLoading, error } = useQuery({
    queryKey: DEVICES_KEY,
    queryFn: signalingApi.listDevices,
    staleTime: 30_000,
  });

  if (isLoading) return <div className="empty-state"><div className="spinner" /></div>;
  if (error) return <p style={{ color: 'var(--ink-500)' }}>Не удалось загрузить устройства.</p>;

  return (
    <div className="settings-section">
      <h3 className="settings-section-title">Устройства</h3>
      {!devices?.length
        ? <p style={{ color: 'var(--ink-500)', fontSize: 13 }}>Нет зарегистрированных устройств.</p>
        : (
          <div className="devices-list">
            {devices.map(d => <DeviceRow key={d.deviceId} device={d} />)}
          </div>
        )
      }
    </div>
  );
}
