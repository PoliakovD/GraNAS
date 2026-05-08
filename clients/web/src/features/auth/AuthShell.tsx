import type { ReactNode } from 'react';
import { Icon } from '../../shared/Icon';

const FEATURES = [
  'Регистрация по email — без OAuth и сторонних сервисов',
  'Папки с любой вложенностью; права View / Full',
  'Временные share-ссылки с TTL и мгновенным отзывом',
  'Web, Windows и Android — единый аккаунт',
];

export function AuthShell({ children }: { children: ReactNode }) {
  return (
    <div className="auth-frame">
      <div className="auth-hero">
        <div className="brand" style={{ padding: 0 }}>
          <div className="brand-mark">G</div>
          <div className="brand-name">GraNAS</div>
        </div>
        <div>
          <h1>Совместный доступ к папкам без файлов на сервере.</h1>
          <p className="tag-line">
            GraNAS управляет правами и ссылками. Содержимое файлов передаётся напрямую между вашими
            устройствами по зашифрованному WebRTC-каналу.
          </p>
          <div className="auth-feature-list">
            {FEATURES.map((text, i) => (
              <div key={i} className="auth-feature">
                <div className="check"><Icon name="check" size={13} /></div>
                <span>{text}</span>
              </div>
            ))}
          </div>
        </div>
        <div style={{ fontSize: 12, color: 'rgba(255,255,255,0.55)' }}>
          GraNAS · файлы остаются у вас
        </div>
      </div>

      <div className="auth-form-pane">
        {children}
      </div>
    </div>
  );
}
