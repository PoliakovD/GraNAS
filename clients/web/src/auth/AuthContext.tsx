import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { authApi } from '../api/auth.api';
import { getAccessToken, registerLogoutCallback, setAccessToken } from '../api/client';
import type { CurrentUser, LoginRequest } from '../types/auth';
import { decodeUser } from './jwt';

interface AuthState {
  user: CurrentUser | null;
  loading: boolean;
}

interface AuthContextValue extends AuthState {
  login: (data: LoginRequest) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({ user: null, loading: true });

  const applyToken = useCallback((token: string | null) => {
    setAccessToken(token);
    setState({ user: token ? (decodeUser(token) ?? null) : null, loading: false });
  }, []);

  // Try silent refresh on mount (restores session if cookie is alive)
  useEffect(() => {
    import('../api/client').then(({ api }) => {
      api.post<{ access_token: string }>('/api/auth/refresh')
        .then(res => applyToken(res.data.access_token))
        .catch(() => setState({ user: null, loading: false }));
    });
  }, [applyToken]);

  useEffect(() => {
    registerLogoutCallback(() => setState({ user: null, loading: false }));
  }, []);

  const login = useCallback(async (data: LoginRequest) => {
    const res = await authApi.login(data);
    applyToken(res.data.access_token);
  }, [applyToken]);

  const logout = useCallback(async () => {
    try { await authApi.logout(); } finally {
      applyToken(null);
    }
  }, [applyToken]);

  return (
    <AuthContext.Provider value={{ ...state, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}

export function useCurrentUser(): CurrentUser {
  const { user } = useAuth();
  if (!user) throw new Error('No authenticated user');
  return user;
}

export { getAccessToken };
