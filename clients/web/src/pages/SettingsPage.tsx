import { Navigate, NavLink, Outlet, Route, Routes } from 'react-router-dom';
import { AccountTab } from './settings/AccountTab';
import { NotificationsTab } from './settings/NotificationsTab';

export function SettingsPage() {
  return (
    <div className="settings-layout">
      <div className="page-head" style={{ marginBottom: 0 }}>
        <div>
          <h1 className="page-title">Настройки</h1>
        </div>
      </div>
      <div className="settings-body">
        <nav className="settings-nav">
          <NavLink to="account"       className={({ isActive }) => `settings-nav-item${isActive ? ' active' : ''}`}>Аккаунт</NavLink>
          <NavLink to="notifications" className={({ isActive }) => `settings-nav-item${isActive ? ' active' : ''}`}>Уведомления</NavLink>
        </nav>
        <div className="settings-content">
          <Routes>
            <Route index element={<Navigate to="account" replace />} />
            <Route path="account"       element={<AccountTab />} />
            <Route path="notifications" element={<NotificationsTab />} />
          </Routes>
        </div>
      </div>
    </div>
  );
}
