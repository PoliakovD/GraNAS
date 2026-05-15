import { useEffect, useState } from 'react';
import { settingsApi, defaultChannelPrefs } from '../../api/settings.api';
import type { NotificationPrefs } from '../../api/settings.api';
import { enablePush, disablePush, isPushEnabled } from '../../lib/pushSubscribe';
import { toast } from '../../shared/useToast';
import { ALL_NOTIFICATION_TYPES, NOTIFICATION_TYPE_LABELS } from '../../types/notification';
import type { NotificationType } from '../../types/notification';

type Channel = 'email' | 'inApp' | 'webPush';

const CHANNEL_LABELS: Record<Channel, string> = {
  email:   'Email',
  inApp:   'В приложении',
  webPush: 'Push',
};

const DEFAULT_PREFS: NotificationPrefs = {
  email:   defaultChannelPrefs(true),
  inApp:   defaultChannelPrefs(true),
  webPush: defaultChannelPrefs(false),
};

export function NotificationsTab() {
  const [prefs, setPrefs] = useState<NotificationPrefs>(DEFAULT_PREFS);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [pushEnabled, setPushEnabled] = useState(false);
  const [pushLoading, setPushLoading] = useState(false);

  useEffect(() => {
    settingsApi.getPrefs()
      .then(data => setPrefs(data.notificationPrefs))
      .catch(() => toast('Не удалось загрузить настройки'))
      .finally(() => setLoading(false));
    setPushEnabled(isPushEnabled());
  }, []);

  const toggle = (channel: Channel, type: NotificationType) => {
    setPrefs(prev => ({
      ...prev,
      [channel]: { ...prev[channel], [type]: !prev[channel][type] },
    }));
  };

  const save = async () => {
    setSaving(true);
    try {
      await settingsApi.updatePrefs(prefs);
      toast('Настройки сохранены');
    } catch {
      toast('Ошибка сохранения');
    } finally {
      setSaving(false);
    }
  };

  const togglePush = async () => {
    setPushLoading(true);
    try {
      if (pushEnabled) {
        await disablePush();
        setPushEnabled(false);
        toast('Push-уведомления отключены');
      } else {
        await enablePush();
        setPushEnabled(true);
        toast('Push-уведомления включены');
      }
    } catch (err) {
      toast((err as Error).message ?? 'Не удалось изменить настройку push');
    } finally {
      setPushLoading(false);
    }
  };

  if (loading) return <div className="empty-state"><div className="spinner" /></div>;

  const channels: Channel[] = ['email', 'inApp', 'webPush'];

  return (
    <div className="settings-section">
      <h3 className="settings-section-title">Уведомления</h3>

      <div className="settings-push-row">
        <div>
          <div style={{ fontWeight: 550, fontSize: 13 }}>Push-уведомления в браузере</div>
          <div style={{ fontSize: 12, color: 'var(--ink-500)', marginTop: 2 }}>
            {pushEnabled ? 'Включены для этого браузера' : 'Отключены — нажмите, чтобы включить'}
          </div>
        </div>
        <button
          className={`btn ${pushEnabled ? 'ghost' : 'brand'} sm`}
          onClick={() => void togglePush()}
          disabled={pushLoading}
        >
          {pushLoading ? '…' : pushEnabled ? 'Отключить' : 'Включить push'}
        </button>
      </div>

      <div className="notif-prefs-table">
        <div className="notif-prefs-header">
          <span>Событие</span>
          {channels.map(ch => <span key={ch}>{CHANNEL_LABELS[ch]}</span>)}
        </div>
        {ALL_NOTIFICATION_TYPES.map(type => (
          <div key={type} className="notif-prefs-row">
            <span>{NOTIFICATION_TYPE_LABELS[type]}</span>
            {channels.map(ch => (
              <label key={ch} className="notif-prefs-cell">
                <input
                  type="checkbox"
                  checked={prefs[ch][type]}
                  onChange={() => toggle(ch, type)}
                />
              </label>
            ))}
          </div>
        ))}
      </div>

      <button
        className="btn brand"
        style={{ marginTop: 20 }}
        onClick={() => void save()}
        disabled={saving}
      >
        {saving ? 'Сохранение…' : 'Сохранить'}
      </button>
    </div>
  );
}
