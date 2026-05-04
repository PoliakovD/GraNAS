import type { LoginRequest, RegisterRequest, RegisterResponse, TokenResponse } from '../types/auth';
import { api } from './client';

export const authApi = {
  register: (data: RegisterRequest) =>
    api.post<RegisterResponse>('/api/auth/register', data),

  login: (data: LoginRequest) =>
    api.post<TokenResponse>('/api/auth/login', data),

  logout: () =>
    api.post<{ message: string }>('/api/auth/logout'),
};
