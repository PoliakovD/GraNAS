import { useRef, useState } from 'react';
import { useAuth } from '../../auth/AuthContext';
import { avatarApi } from '../../api/avatar.api';
import { useAvatarUrl } from '../../shared/useAvatarUrl';
import { toast } from '../../shared/useToast';
import { initials, colorFromString } from '../../shared/format';

const MAX_AVATAR_BYTES = 256 * 1024;
const ALLOWED_TYPES = ['image/png', 'image/jpeg', 'image/webp'];

export function AccountTab() {
  const { user, loading } = useAuth();
  if (loading || !user) return <div className="empty-state"><div className="spinner" /></div>;
  const fileRef = useRef<HTMLInputElement>(null);
  const [avatarKey, setAvatarKey] = useState<string | null>(null);
  const avatarUrl = useAvatarUrl(avatarKey);
  const [uploading, setUploading] = useState(false);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (file.size > MAX_AVATAR_BYTES) { toast('Аватар не должен превышать 256 КБ'); return; }
    if (!ALLOWED_TYPES.includes(file.type)) { toast('Допустимые форматы: PNG, JPEG, WebP'); return; }

    setUploading(true);
    try {
      await avatarApi.upload(file);
      setAvatarKey(Date.now().toString());
      toast('Аватар обновлён');
    } catch {
      toast('Не удалось загрузить аватар');
    } finally {
      setUploading(false);
      if (fileRef.current) fileRef.current.value = '';
    }
  };

  const handleRemove = async () => {
    setUploading(true);
    try {
      await avatarApi.remove();
      setAvatarKey(null);
      toast('Аватар удалён');
    } catch {
      toast('Не удалось удалить аватар');
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="settings-section">
      <h3 className="settings-section-title">Аккаунт</h3>

      <div className="avatar-edit-block">
        {avatarUrl
          ? <img className="avatar-large" src={avatarUrl} alt="Аватар" />
          : (
            <div
              className="avatar-large avatar-initials"
              style={{ background: colorFromString(user.email) }}
            >
              {initials(user.email)}
            </div>
          )
        }
        <div className="avatar-edit-actions">
          <input
            ref={fileRef}
            type="file"
            accept="image/png,image/jpeg,image/webp"
            style={{ display: 'none' }}
            onChange={e => void handleFileChange(e)}
          />
          <button
            className="btn brand sm"
            onClick={() => fileRef.current?.click()}
            disabled={uploading}
          >
            {uploading ? '…' : 'Загрузить фото'}
          </button>
          {avatarUrl && (
            <button
              className="btn ghost sm"
              onClick={() => void handleRemove()}
              disabled={uploading}
            >
              Удалить
            </button>
          )}
          <p style={{ fontSize: 11, color: 'var(--ink-400)', marginTop: 4 }}>PNG, JPEG или WebP · до 256 КБ</p>
        </div>
      </div>

      <div className="settings-field-group" style={{ marginTop: 24 }}>
        <div className="settings-field-row">
          <span className="settings-field-label">Email</span>
          <span className="settings-field-value">{user.email}</span>
        </div>
        <div className="settings-field-row">
          <span className="settings-field-label">Идентификатор</span>
          <span className="settings-field-value" style={{ fontFamily: 'var(--font-mono)', fontSize: 12 }}>{user.id}</span>
        </div>
      </div>
    </div>
  );
}
