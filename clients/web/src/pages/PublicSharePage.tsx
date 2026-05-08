import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { sharesApi } from '../api/shares.api';
import { FileListPanel } from '../features/p2p/FileListPanel';
import { Icon } from '../shared/Icon';
import { relTime } from '../shared/format';

export function PublicSharePage() {
  const { token } = useParams<{ token: string }>();

  const { data, isLoading, error } = useQuery({
    queryKey: ['public-share', token],
    queryFn: () => sharesApi.getByToken(token!).then(r => r.data),
    enabled: !!token,
    retry: false,
  });

  if (isLoading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh', background: 'var(--bg)' }}>
        <div style={{ textAlign: 'center', color: 'var(--ink-500)' }}>
          <div className="sk" style={{ width: 48, height: 48, borderRadius: '50%', margin: '0 auto 16px' }} />
          <div>Загрузка…</div>
        </div>
      </div>
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const status = (error as any)?.response?.status;

  const ErrorView = ({ icon, title, desc }: { icon: Parameters<typeof Icon>[0]['name']; title: string; desc: string }) => (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh', background: 'var(--bg)' }}>
      <div className="empty">
        <div className="glyph" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }}>
          <Icon name={icon} size={28} />
        </div>
        <h4>{title}</h4>
        <p>{desc}</p>
      </div>
    </div>
  );

  if (status === 410) return <ErrorView icon="link" title="Ссылка отозвана" desc="Владелец отозвал эту ссылку доступа." />;
  if (status === 404) return <ErrorView icon="link" title="Ссылка не найдена" desc="Ссылка не существует или истёк срок действия." />;
  if (error || !data) return <ErrorView icon="circle" title="Ошибка" desc="Не удалось загрузить содержимое ссылки." />;

  return (
    <div style={{ background: 'var(--bg)', minHeight: '100vh', padding: '40px 20px' }}>
      <div style={{ maxWidth: 700, margin: '0 auto' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 24 }}>
          <div className="brand-mark">G</div>
          <div className="brand-name">GraNAS</div>
        </div>

        <div style={{
          background: 'var(--surface)',
          border: '1px solid var(--ink-100)',
          borderRadius: 'var(--r-lg)',
          padding: 20,
          marginBottom: 16,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 12 }}>
            <div style={{
              width: 40, height: 40, borderRadius: 10,
              background: 'var(--brand-primary-soft)', color: 'var(--brand-primary)',
              display: 'grid', placeItems: 'center',
            }}>
              <Icon name="folder" size={20} />
            </div>
            <div>
              <div style={{ fontWeight: 600, fontSize: 18 }}>{data.folderName}</div>
              {data.path && (
                <div style={{ fontSize: 13, color: 'var(--ink-500)', marginTop: 2 }}>
                  Доступ ограничен: {data.path}
                </div>
              )}
            </div>
          </div>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', fontSize: 12.5, color: 'var(--ink-500)' }}>
            <span className="tag">
              <Icon name="shield" size={11} /> публичная ссылка
            </span>
            {data.expiresAt && (
              <span className="tag amber">
                истекает {relTime(data.expiresAt)}
              </span>
            )}
          </div>
        </div>

        <div style={{
          background: 'var(--surface)',
          border: '1px solid var(--ink-100)',
          borderRadius: 'var(--r-lg)',
          padding: 20,
        }}>
          <div style={{ fontWeight: 600, marginBottom: 16 }}>Файлы</div>
          <FileListPanel folderId={data.folderId} shareToken={token} />
        </div>
      </div>
    </div>
  );
}
