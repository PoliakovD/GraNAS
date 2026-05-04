import type { GrantPermissionRequest, PermissionResponse } from '../types/permission';
import { api } from './client';

export const permissionsApi = {
  grant: (folderId: string, data: GrantPermissionRequest) =>
    api.post<PermissionResponse>(`/api/metadata/folders/${folderId}/permissions`, data),

  revoke: (folderId: string, userId: string) =>
    api.delete(`/api/metadata/folders/${folderId}/permissions/${userId}`),
};
