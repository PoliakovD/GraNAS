import { jwtDecode } from 'jwt-decode';
import type { CurrentUser } from '../types/auth';

interface JwtPayload {
  sub?: string;
  email?: string;
  exp?: number;
}

export function decodeUser(token: string): CurrentUser | null {
  try {
    const payload = jwtDecode<JwtPayload>(token);
    if (!payload.sub) return null;
    return { id: payload.sub, email: payload.email ?? '' };
  } catch {
    return null;
  }
}
