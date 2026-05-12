import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { authApi } from '../api/auth.api';
import { AuthShell } from '../features/auth/AuthShell';
import { Icon } from '../shared/Icon';

export function RegisterPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [emailConsent, setEmailConsent] = useState(true);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) { setError('Заполните все поля'); return; }
    if (password.length < 8) { setError('Пароль должен быть не менее 8 символов'); return; }
    setLoading(true);
    setError('');
    try {
      await authApi.register({ email, password, emailNotificationsConsent: emailConsent });
      await login({ email, password });
      navigate('/', { replace: true });
    } catch (err) {
      const msg =
        (err as { response?: { data?: { title?: string } } })?.response?.data?.title ??
        'Попробуйте позже';
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthShell>
      <div style={{
        display: 'inline-flex',
        background: 'var(--surface-2)',
        padding: 3,
        borderRadius: 9,
        marginBottom: 24,
        alignSelf: 'flex-start',
      }}>
        <Link to="/login" style={{
          padding: '6px 14px', borderRadius: 6, fontSize: 13, fontWeight: 550,
          color: 'var(--ink-500)', textDecoration: 'none',
        }}>Вход</Link>
        <span style={{
          padding: '6px 14px', borderRadius: 6, fontSize: 13, fontWeight: 550,
          background: 'var(--surface)', boxShadow: 'var(--shadow-sm)',
          color: 'var(--ink-900)',
        }}>Регистрация</span>
      </div>

      <h2>Создать аккаунт</h2>
      <p className="auth-sub">Минимум данных: email и пароль.</p>

      <form onSubmit={e => void onSubmit(e)} noValidate>
        <div className="field">
          <label htmlFor="reg-email">Email</label>
          <input
            id="reg-email"
            type="email"
            placeholder="you@company.com"
            autoFocus
            value={email}
            onChange={e => setEmail(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="reg-password">Пароль</label>
          <input
            id="reg-password"
            type="password"
            placeholder="Минимум 8 символов"
            value={password}
            onChange={e => setPassword(e.target.value)}
          />
        </div>

        <div className="field" style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
          <input
            id="reg-consent"
            type="checkbox"
            checked={emailConsent}
            onChange={e => setEmailConsent(e.target.checked)}
            style={{ width: 14, height: 14, flexShrink: 0 }}
          />
          <label htmlFor="reg-consent" style={{ fontSize: 12.5, color: 'var(--ink-600)', cursor: 'pointer', marginBottom: 0 }}>
            Получать уведомления на email
          </label>
        </div>

        {error && <div className="field-error" style={{ marginBottom: 12 }}>{error}</div>}

        <button
          type="submit"
          className="btn brand"
          disabled={loading}
          style={{ width: '100%', justifyContent: 'center', padding: 12, fontSize: 14 }}
        >
          {loading ? 'Загрузка…' : <>Зарегистрироваться <Icon name="arrow-right" size={14} /></>}
        </button>
      </form>

      <div style={{ marginTop: 18, fontSize: 12.5, color: 'var(--ink-500)', textAlign: 'center' }}>
        Файлы никогда не попадают на сервер.
      </div>
    </AuthShell>
  );
}
