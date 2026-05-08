import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Icon } from '../../shared/Icon';
import { initials, colorFromString, relTime } from '../../shared/format';
import { permissionsKey, useGrantPermission, useRevokePermission } from '../permissions/usePermissionMutations';
import { useSharesQuery, useRevokeShare } from '../shares/useShareMutations';
import type { FolderResponse } from '../../types/folder';
import type { PermissionResponse } from '../../types/permission';
import type { ShareLinkResponse } from '../../types/share';
import type { AccessLevel } from '../../types/folder';

function ShareLinkCard({ share, isOwner, onRevoke, onCopy }: {
  share: ShareLinkResponse;
  isOwner: boolean;
  onRevoke: (id: string) => void;
  onCopy: (url: string) => void;
}) {
  const expIn = share.expiresAt
    ? Math.max(0, Math.floor((+new Date(share.expiresAt) - Date.now()) / 1000 / 3600))
    : null;
  const expClass = expIn !== null && expIn < 24 ? 'amber' : 'green';

  return (
    <div className="share-card">
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <span className="tag green"><span className="live-dot" />активна</span>
        {expIn !== null && (
          <span className={`tag ${expClass}`}>
            {expIn < 1 ? 'истекает скоро' : `истекает через ${expIn} ч`}
          </span>
        )}
      </div>
      <div className="share-link-input">
        <Icon name="link" size={13} />
        <code style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {share.shareUrl || `granas.io/s/… (${share.id.slice(0, 8)})`}
        </code>
        <button
          className="btn ghost sm"
          disabled={!share.shareUrl}
          onClick={() => share.shareUrl && onCopy(share.shareUrl)}
        >
          <Icon name="copy" size={12} /> Копировать
        </button>
      </div>
      {share.path && (
        <div className="share-meta">
          <Icon name="file" size={11} /> только: {share.path}
        </div>
      )}
      {isOwner && (
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <button className="btn danger sm" onClick={() => onRevoke(share.id)}>
            <Icon name="trash" size={12} /> Отозвать
          </button>
        </div>
      )}
    </div>
  );
}

interface InspectorProps {
  folder: FolderResponse;
  isOwner: boolean;
  onCreateShare: () => void;
}

export function Inspector({ folder, isOwner, onCreateShare }: InspectorProps) {
  const [tab, setTab] = useState<'people' | 'links' | 'info'>('people');
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteRole, setInviteRole] = useState<AccessLevel>('View');

  const qc = useQueryClient();
  const permissions = qc.getQueryData<PermissionResponse[]>(permissionsKey(folder.id)) ?? [];
  const grant = useGrantPermission(folder.id);
  const revoke = useRevokePermission(folder.id);
  const { data: shares = [] } = useSharesQuery(folder.id);
  const revokeShare = useRevokeShare(folder.id);

  const activeShares = shares.filter(s => !s.revoked);

  const handleGrant = async () => {
    if (!inviteEmail.trim()) return;
    await grant.mutateAsync({ email: inviteEmail.trim(), accessLevel: inviteRole });
    setInviteEmail('');
  };

  const handleCopyLink = (url: string) => {
    navigator.clipboard.writeText(url).catch(() => {});
  };

  return (
    <aside className="inspector">
      <div className="inspector-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <div className="icon-wrap" style={{ width: 40, height: 40, borderRadius: 10 }}>
            <Icon name="folder" size={20} />
          </div>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div className="name">{folder.name}</div>
            <div className="sub">{relTime(folder.updatedAt)}</div>
          </div>
        </div>
      </div>

      <div className="inspector-tabs">
        <button
          className={`inspector-tab${tab === 'people' ? ' on' : ''}`}
          onClick={() => setTab('people')}
        >
          <Icon name="user" size={13} /> Доступ
          <span style={{
            background: tab === 'people' ? 'rgba(255,255,255,0.2)' : 'var(--ink-100)',
            color: 'inherit',
            borderRadius: 999,
            padding: '0 6px',
            fontSize: 11,
          }}>
            {permissions.length}
          </span>
        </button>
        <button
          className={`inspector-tab${tab === 'links' ? ' on' : ''}`}
          onClick={() => setTab('links')}
        >
          <Icon name="link" size={13} /> Ссылки
          <span style={{
            background: tab === 'links' ? 'rgba(255,255,255,0.2)' : 'var(--ink-100)',
            color: 'inherit',
            borderRadius: 999,
            padding: '0 6px',
            fontSize: 11,
          }}>
            {activeShares.length}
          </span>
        </button>
        <button
          className={`inspector-tab${tab === 'info' ? ' on' : ''}`}
          onClick={() => setTab('info')}
        >
          <Icon name="circle" size={13} /> Свойства
        </button>
      </div>

      <div className="inspector-body">
        {tab === 'people' && (
          <>
            {isOwner && (
              <div>
                <div className="section-title">Пригласить</div>
                <div className="invite-row">
                  <input
                    type="email"
                    placeholder="email@company.com"
                    value={inviteEmail}
                    onChange={e => setInviteEmail(e.target.value)}
                    onKeyDown={e => { if (e.key === 'Enter') void handleGrant(); }}
                  />
                  <select
                    value={inviteRole}
                    onChange={e => setInviteRole(e.target.value as AccessLevel)}
                  >
                    <option value="View">Чтение</option>
                    <option value="Full">Полный</option>
                  </select>
                  <button
                    className="btn brand sm"
                    onClick={() => void handleGrant()}
                    disabled={!inviteEmail.trim() || grant.isPending}
                  >
                    <Icon name="send" size={12} /> Выдать
                  </button>
                </div>
                <div style={{ fontSize: 11.5, color: 'var(--ink-400)', marginTop: 8, lineHeight: 1.4 }}>
                  Пользователь получит уведомление и увидит папку во вкладке «Доступные».
                </div>
              </div>
            )}

            <div>
              <div className="section-title">Кто имеет доступ</div>
              {permissions.length === 0 && !isOwner && (
                <div style={{ fontSize: 12.5, color: 'var(--ink-500)', padding: '8px 0' }}>
                  Только владелец имеет доступ.
                </div>
              )}
              {permissions.map(p => (
                <div key={p.userId} className="person-row">
                  <div className="av" style={{ background: colorFromString(p.email ?? p.userId) }}>
                    {initials(p.email ?? p.userId)}
                  </div>
                  <div className="who">
                    <div className="email">{p.email ?? p.userId}</div>
                    <div className="role">{p.accessLevel === 'Full' ? 'Полный доступ' : 'Только чтение'}</div>
                  </div>
                  <span className={`tag ${p.accessLevel === 'Full' ? 'green' : 'blue'}`}>
                    {p.accessLevel === 'Full' ? 'Полный' : 'Чтение'}
                  </span>
                  {isOwner && (
                    <button
                      className="icon-btn"
                      style={{ width: 26, height: 26 }}
                      onClick={() => revoke.mutate(p.userId)}
                      title="Отозвать доступ"
                    >
                      <Icon name="trash" size={13} />
                    </button>
                  )}
                </div>
              ))}
            </div>
          </>
        )}

        {tab === 'links' && (
          <>
            {isOwner && (
              <button className="btn brand" onClick={onCreateShare} style={{ width: '100%', justifyContent: 'center' }}>
                <Icon name="plus" size={14} /> Создать share-ссылку
              </button>
            )}
            <div>
              <div className="section-title">Активные ссылки</div>
              {activeShares.length === 0 && (
                <div style={{ padding: 12, fontSize: 12.5, color: 'var(--ink-500)', background: 'var(--surface-2)', borderRadius: 10, lineHeight: 1.4 }}>
                  Ссылок пока нет. Создайте временную ссылку, чтобы поделиться папкой с теми, у кого нет аккаунта.
                </div>
              )}
              {activeShares.map(s => (
                <ShareLinkCard
                  key={s.id}
                  share={s}
                  isOwner={isOwner}
                  onRevoke={id => revokeShare.mutate(id)}
                  onCopy={handleCopyLink}
                />
              ))}
            </div>
          </>
        )}

        {tab === 'info' && (
          <>
            <div>
              <div className="section-title">О папке</div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10, fontSize: 13 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <span style={{ color: 'var(--ink-500)' }}>ID</span>
                  <code style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5 }}>{folder.id.slice(0, 8)}…</code>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <span style={{ color: 'var(--ink-500)' }}>Создана</span>
                  <span>{new Date(folder.createdAt).toLocaleDateString('ru-RU')}</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <span style={{ color: 'var(--ink-500)' }}>Изменена</span>
                  <span>{relTime(folder.updatedAt)}</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <span style={{ color: 'var(--ink-500)' }}>Владелец</span>
                  <span>{folder.ownerEmail ?? (folder.ownerId.slice(0, 8) + '…')}</span>
                </div>
              </div>
            </div>
            <div>
              <div className="section-title">Безопасность</div>
              <div style={{ background: 'var(--surface-2)', padding: 12, borderRadius: 10, display: 'flex', gap: 10 }}>
                <Icon name="shield" size={16} />
                <div style={{ fontSize: 12.5, lineHeight: 1.5, color: 'var(--ink-700)' }}>
                  Метаданные хранятся на сервере. Содержимое файлов передаётся напрямую между устройствами по зашифрованному WebRTC-каналу.
                </div>
              </div>
            </div>
          </>
        )}
      </div>
    </aside>
  );
}
