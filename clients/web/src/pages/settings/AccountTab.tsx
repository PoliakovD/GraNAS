import { useCurrentUser } from '../../auth/AuthContext';

export function AccountTab() {
  const user = useCurrentUser();
  return (
    <div className="settings-section">
      <h3 className="settings-section-title">Аккаунт</h3>
      <div className="settings-field-group">
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
