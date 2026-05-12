import { useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useAuth, useCurrentUser } from '../auth/AuthContext';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { useMarkAllRead, useMarkRead, useNotificationsList, useUnreadCount } from '../notifications/useNotifications';
import type { NotificationDto } from '../api/notifications.api';
import { Icon } from './Icon';
import { initials, relTime } from './format';
import { useClickOutside } from './useClickOutside';
import type { FolderResponse } from '../types/folder';

function buildAncestors(folders: FolderResponse[], folderId: string): FolderResponse[] {
  const map = new Map(folders.map(f => [f.id, f]));
  const chain: FolderResponse[] = [];
  let current = map.get(folderId);
  while (current) {
    chain.unshift(current);
    current = current.parentFolderId ? map.get(current.parentFolderId) : undefined;
  }
  return chain;
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

function iconFor(type: string): { name: Parameters<typeof Icon>[0]['name']; bg: string; fg: string } {
  if (type === 'permission' || type === 'PermissionGranted') return { name: 'shield', bg: 'var(--brand-primary-soft)', fg: 'var(--brand-primary)' };
  if (type === 'share' || type === 'ShareLinkAccessed') return { name: 'link', bg: 'var(--info-soft)', fg: 'var(--info)' };
  if (type === 'p2p' || type === 'OwnerOnline') return { name: 'wifi', bg: 'var(--success-soft)', fg: 'var(--success)' };
  return { name: 'bell', bg: 'var(--surface-2)', fg: 'var(--ink-500)' };
}

function NotifPopover({ onClose }: { onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null);
  useClickOutside(ref, onClose);
  const { data, isLoading } = useNotificationsList();
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();
  const { data: countData } = useUnreadCount();

  const items = data?.pages.flatMap(p => p.items) ?? [];
  const unread = countData?.unreadCount ?? 0;

  return (
    <div ref={ref} className="notif-popover">
      <div className="notif-head">
        <h4>
          Уведомления
          {unread > 0 && <span className="tag brand">{unread} новых</span>}
        </h4>
        <button className="btn ghost sm" onClick={() => markAll.mutate()}>Прочитать все</button>
      </div>
      <div className="notif-list">
        {isLoading && (
          <div style={{ padding: 20, textAlign: 'center', color: 'var(--ink-400)', fontSize: 13 }}>Загрузка…</div>
        )}
        {!isLoading && items.length === 0 && (
          <div style={{ padding: 20, textAlign: 'center', color: 'var(--ink-400)', fontSize: 13 }}>Уведомлений нет</div>
        )}
        {items.map(n => {
          const ic = iconFor(n.type);
          return (
            <div
              key={n.id}
              className={`notif-item${n.isRead ? '' : ' unread'}`}
              onClick={() => markRead.mutate(n.id)}
            >
              <div className="icon" style={{ background: ic.bg, color: ic.fg }}>
                <Icon name={ic.name} size={15} />
              </div>
              <div className="body">
                <div className="title">{notifTitle(n)}</div>
                <div className="desc">{notifDesc(n)}</div>
                <div className="when">{relTime(n.createdAt)}</div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function AccountMenu({ onClose }: { onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null);
  useClickOutside(ref, onClose);
  const user = useCurrentUser();
  const { logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  return (
    <div ref={ref} className="account-menu">
      <div className="account-menu-header">
        <div className="avatar" style={{ width: 36, height: 36, fontSize: 14 }}>{initials(user.email)}</div>
        <div style={{ minWidth: 0 }}>
          <div style={{ fontWeight: 600, fontSize: 13 }}>{user.email.split('@')[0]}</div>
          <div style={{ fontSize: 11.5, color: 'var(--ink-500)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{user.email}</div>
        </div>
      </div>
      <div className="ctx-item" onClick={() => { onClose(); navigate('/settings/account'); }}><Icon name="user" size={14} /> Профиль</div>
      <div className="ctx-item" onClick={() => { onClose(); navigate('/settings/notifications'); }}><Icon name="settings" size={14} /> Настройки</div>
      <div className="ctx-sep" />
      <div className="ctx-item danger" onClick={() => void handleLogout()}>
        <Icon name="logout" size={14} /> Выйти
      </div>
    </div>
  );
}

interface CrumbItem {
  label: string;
  icon?: Parameters<typeof Icon>[0]['name'];
  to?: string;
}

export function Topbar() {
  const [showNotif, setShowNotif] = useState(false);
  const [showAcct, setShowAcct] = useState(false);
  const user = useCurrentUser();
  const location = useLocation();
  const navigate = useNavigate();
  const { id: folderId } = useParams<{ id: string }>();
  const { data: folders = [] } = useFoldersQuery();
  const { data: countData } = useUnreadCount();
  const unread = countData?.unreadCount ?? 0;

  const crumbs: CrumbItem[] = (() => {
    const p = location.pathname;
    if (p === '/') return [{ label: 'Главная', icon: 'home' }];
    if (p === '/folders') return [{ label: 'Мои папки', icon: 'folder' }];
    if (p === '/shared') return [{ label: 'Доступные', icon: 'shared' }];
    if (p === '/links') return [{ label: 'Ссылки', icon: 'link' }];
    if (p === '/recent') return [{ label: 'Недавние', icon: 'recent' }];
    if (folderId) {
      const folder = folders.find(f => f.id === folderId);
      if (!folder) return [{ label: 'Папка' }];
      const isOwner = folder.ownerId === user.id;
      const ancestors = buildAncestors(folders, folderId);
      const root: CrumbItem = isOwner
        ? { label: 'Мои папки', icon: 'folder', to: '/folders' }
        : { label: 'Доступные', icon: 'shared', to: '/shared' };
      return [
        root,
        ...ancestors.slice(0, -1).map(a => ({ label: a.name, to: `/folders/${a.id}` })),
        { label: ancestors[ancestors.length - 1]?.name ?? folder.name },
      ];
    }
    return [];
  })();

  return (
    <div className="topbar">
      <div className="crumbs">
        {crumbs.map((c, i) => (
          <span key={i} style={{ display: 'contents' }}>
            {i > 0 && <span className="sep"><Icon name="chevron-right" size={12} /></span>}
            <button
              className={`crumb${i === crumbs.length - 1 ? ' current' : ''}`}
              onClick={() => c.to && navigate(c.to)}
              style={{ cursor: c.to ? 'pointer' : 'default' }}
            >
              {c.icon && <Icon name={c.icon} size={12} />}
              {c.label}
            </button>
          </span>
        ))}
      </div>

      <div className="topbar-spacer" />

      <button className="icon-btn" onClick={() => { setShowNotif(s => !s); setShowAcct(false); }}>
        <Icon name="bell" size={18} />
        {unread > 0 && <span className="dot" />}
      </button>
      <div
        className="avatar"
        onClick={() => { setShowAcct(s => !s); setShowNotif(false); }}
        title={user.email}
      >
        {initials(user.email)}
      </div>

      {showNotif && <NotifPopover onClose={() => setShowNotif(false)} />}
      {showAcct && <AccountMenu onClose={() => setShowAcct(false)} />}
    </div>
  );
}
