import { useNavigate, useOutletContext } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { FolderGrid } from '../features/folders/FolderGrid';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { useNotificationsList } from '../notifications/useNotifications';
import { useGlobalSharesQuery } from '../features/shares/useGlobalSharesQuery';
import { Icon } from '../shared/Icon';
import { relTime } from '../shared/format';
import type { FolderResponse } from '../types/folder';
import type { NotificationDto } from '../api/notifications.api';

type OutletCtx = { openContext: (f: FolderResponse, x: number, y: number) => void };

function iconFor(type: string): { name: Parameters<typeof Icon>[0]['name']; bg: string; fg: string } {
  if (type === 'permission' || type === 'PermissionGranted') return { name: 'shield', bg: 'var(--brand-primary-soft)', fg: 'var(--brand-primary)' };
  if (type === 'share' || type === 'ShareLinkAccessed') return { name: 'link', bg: 'var(--info-soft)', fg: 'var(--info)' };
  if (type === 'p2p' || type === 'OwnerOnline') return { name: 'wifi', bg: 'var(--success-soft)', fg: 'var(--success)' };
  return { name: 'bell', bg: 'var(--surface-2)', fg: 'var(--ink-500)' };
}

function notifTitle(n: NotificationDto): string {
  const d = n.data;
  if (typeof d.title === 'string') return d.title;
  if (typeof d.folderName === 'string') return d.folderName;
  return n.type;
}

function notifDesc(n: NotificationDto): string {
  const d = n.data;
  if (typeof d.description === 'string') return d.description;
  if (typeof d.body === 'string') return d.body;
  if (typeof d.message === 'string') return d.message;
  return '';
}

export function HomePage() {
  const user = useCurrentUser();
  const navigate = useNavigate();
  const { data: folders = [] } = useFoldersQuery();
  const { openContext } = useOutletContext<OutletCtx>();
  const { data: notifData } = useNotificationsList();
  const { data: allShares } = useGlobalSharesQuery();

  const owned = folders.filter(f => f.ownerId === user.id);
  const shared = folders.filter(f => f.ownerId !== user.id);
  const recent = [...folders]
    .sort((a, b) => +new Date(b.updatedAt ?? 0) - +new Date(a.updatedAt ?? 0))
    .slice(0, 6);

  const activeLinks = allShares ? allShares.filter(s => !s.revoked).length : null;
  const notifications = notifData?.pages.flatMap(p => p.items).slice(0, 4) ?? [];

  const stats = [
    { label: 'Мои папки', value: owned.length, icon: 'folder' as const, color: '#6938EF' },
    { label: 'Доступные мне', value: shared.length, icon: 'shared' as const, color: '#00C2A8' },
    { label: 'Активные ссылки', value: activeLinks, icon: 'link' as const, color: '#F79009' },
    { label: 'Раздаю доступ', value: null, icon: 'globe' as const, color: '#2E90FA' },
  ];

  const firstName = user.email.split('@')[0].split('.')[0];

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Привет, {firstName}</h1>
          <p className="page-sub">Краткая сводка по вашим папкам и совместному доступу</p>
        </div>
        <div className="head-actions">
          <button className="btn brand" onClick={() => navigate('/folders')}>
            <Icon name="plus" size={14} /> Новая папка
          </button>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14, marginBottom: 28 }}>
        {stats.map(s => (
          <div key={s.label} style={{
            background: 'var(--surface)',
            border: '1px solid var(--ink-100)',
            borderRadius: 'var(--r-lg)',
            padding: 18,
            display: 'flex',
            flexDirection: 'column',
            gap: 12,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <div style={{
                width: 32, height: 32, borderRadius: 8,
                display: 'grid', placeItems: 'center',
                background: s.color + '1F',
                color: s.color,
              }}>
                <Icon name={s.icon} size={16} />
              </div>
              <span style={{ fontSize: 12.5, color: 'var(--ink-500)' }}>{s.label}</span>
            </div>
            <div style={{ fontSize: 28, fontWeight: 700, letterSpacing: '-0.02em' }}>
              {s.value !== null ? s.value : <span style={{ fontSize: 18, color: 'var(--ink-400)' }}>—</span>}
            </div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1.6fr 1fr', gap: 18 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 12 }}>
            <h3 style={{ fontSize: 16, fontWeight: 600, margin: 0 }}>Недавние</h3>
            <button className="btn ghost sm" onClick={() => navigate('/recent')}>
              Все <Icon name="arrow-right" size={12} />
            </button>
          </div>
          {recent.length === 0 ? (
            <div className="empty" style={{ padding: '24px 20px' }}>
              <div className="glyph" style={{ width: 48, height: 48 }}><Icon name="folder" size={22} /></div>
              <p>Ваши папки появятся здесь.</p>
            </div>
          ) : (
            <FolderGrid
              folders={recent}
              currentUserId={user.id}
              onOpen={f => navigate(`/folders/${f.id}`)}
              onSelect={() => {}}
              onContext={openContext}
            />
          )}
        </div>

        <div>
          <h3 style={{ fontSize: 16, fontWeight: 600, margin: '0 0 12px' }}>Что нового</h3>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--ink-100)', borderRadius: 'var(--r-lg)' }}>
            {notifications.length === 0 && (
              <div style={{ padding: 20, textAlign: 'center', fontSize: 13, color: 'var(--ink-400)' }}>
                Уведомлений нет
              </div>
            )}
            {notifications.map((n, i) => {
              const ic = iconFor(n.type);
              return (
                <div key={n.id} style={{
                  display: 'flex', gap: 12, padding: 14,
                  borderBottom: i < notifications.length - 1 ? '1px solid var(--ink-100)' : 'none',
                }}>
                  <div className="icon" style={{
                    width: 30, height: 30, borderRadius: 8,
                    display: 'grid', placeItems: 'center',
                    background: ic.bg, color: ic.fg,
                    flexShrink: 0,
                  }}>
                    <Icon name={ic.name} size={14} />
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <div style={{ fontSize: 13, fontWeight: 550, lineHeight: 1.3 }}>{notifTitle(n)}</div>
                    <div style={{ fontSize: 12, color: 'var(--ink-500)', marginTop: 2 }}>{notifDesc(n)}</div>
                    <div style={{ fontSize: 11, color: 'var(--ink-400)', marginTop: 4 }}>{relTime(n.createdAt)}</div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </>
  );
}
