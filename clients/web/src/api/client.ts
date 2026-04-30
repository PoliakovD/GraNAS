import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';

let _accessToken: string | null = null;
let _onLogout: (() => void) | null = null;
let _refreshPromise: Promise<string | null> | null = null;

export const setAccessToken = (token: string | null): void => { _accessToken = token; };
export const getAccessToken = (): string | null => _accessToken;
export const registerLogoutCallback = (cb: () => void): void => { _onLogout = cb; };

const BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? 'http://localhost:8080';

export const api = axios.create({
  baseURL: BASE_URL,
  withCredentials: true,
});

// separate instance without interceptors — used only for token refresh
const refreshClient = axios.create({ baseURL: BASE_URL, withCredentials: true });

async function doRefresh(): Promise<string | null> {
  try {
    const res = await refreshClient.post<{ access_token: string }>('/api/auth/refresh');
    _accessToken = res.data.access_token;
    return _accessToken;
  } catch {
    _accessToken = null;
    _onLogout?.();
    return null;
  }
}

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (_accessToken) {
    config.headers.Authorization = `Bearer ${_accessToken}`;
  }
  return config;
});

api.interceptors.response.use(
  res => res,
  async (error: AxiosError) => {
    const config = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
    if (error.response?.status === 401 && !config._retry) {
      config._retry = true;
      if (!_refreshPromise) {
        _refreshPromise = doRefresh().finally(() => { _refreshPromise = null; });
      }
      const newToken = await _refreshPromise;
      if (newToken) {
        config.headers.Authorization = `Bearer ${newToken}`;
        return api(config);
      }
    }
    return Promise.reject(error);
  },
);
