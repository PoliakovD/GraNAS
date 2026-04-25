import type { AccessLevel } from './folder';

export interface PermissionResponse {
  userId: string;
  accessLevel: AccessLevel;
  path: string | null;
  createdAt: string;
}

export interface GrantPermissionRequest {
  email: string;
  accessLevel: AccessLevel;
  path?: string | null;
}
