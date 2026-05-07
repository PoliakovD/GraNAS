import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { AuthShell } from '../features/auth/AuthShell';
import { Icon } from '../shared/Icon';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) { setError('Заполните все поля'); return; }
    setLoading(true);
    setError('');
    try {
      await login({ email, password });
      navigate('/', { replace: true });
    } catch (err) {
      const msg =
        (err as { response?: { data?: { title?: string } } })?.response?.data?.title ??
        'Проверьте email и пароль';
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
        <span style={{
          padding: '6px 14px', borderRadius: 6, fontSize: 13, fontWeight: 550,
          background: 'var(--surface)', boxShadow: 'var(--shadow-sm)',
          color: 'var(--ink-900)',
        }}>Вход</span>
        <Link to="/register" style={{
          padding: '6px 14px', borderRadius: 6, fontSize: 13, fontWeight: 550,
          color: 'var(--ink-500)', textDecoration: 'none',
        }}>Регистрация</Link>
      </div>

      <h2>С возвращением</h2>
      <p className="auth-sub">Войдите, чтобы продолжить работу с вашими папками.</p>

      <form onSubmit={e => void onSubmit(e)} noValidate>
        <div className="field">
          <label htmlFor="login-email">Email</label>
          <input
            id="login-email"
            type="email"
            placeholder="you@company.com"
            autoFocus
            value={email}
            onChange={e => setEmail(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="login-password">Пароль</label>
          <input
            id="login-password"
            type="password"
            placeholder="Минимум 8 символов"
            value={password}
            onChange={e => setPassword(e.target.value)}
          />
        </div>

        {error && <div className="field-error" style={{ marginBottom: 12 }}>{error}</div>}

        <button
          type="submit"
          className="btn brand"
          disabled={loading}
          style={{ width: '100%', justifyContent: 'center', padding: 12, fontSize: 14 }}
        >
          {loading ? 'Загрузка…' : <>Войти <Icon name="arrow-right" size={14} /></>}
        </button>
      </form>

      <div style={{ marginTop: 18, fontSize: 12.5, color: 'var(--ink-500)', textAlign: 'center' }}>
        Файлы никогда не попадают на сервер.
      </div>
    </AuthShell>
  );
}
