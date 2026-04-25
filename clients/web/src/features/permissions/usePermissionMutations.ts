import { useMutation, useQueryClient } from '@tanstack/react-query';
import { notification } from 'antd';
import { permissionsApi } from '../../api/permissions.api';
import type { GrantPermissionRequest, PermissionResponse } from '../../types/permission';

export function permissionsKey(folderId: string) {
  return ['permissions', folderId] as const;
}

export function useGrantPermission(folderId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: GrantPermissionRequest) =>
      permissionsApi.grant(folderId, data).then(r => r.data),
    onSuccess: (perm: PermissionResponse, variables: GrantPermissionRequest) => {
      // Optimistically add to cache; backend lacks GET listing endpoint.
      // Store email from form input so it can be displayed instead of userId.
      qc.setQueryData<PermissionResponse[]>(permissionsKey(folderId), prev =>
        [...(prev ?? []).filter(p => p.userId !== perm.userId), { ...perm, email: variables.email }]
      );
      notification.success({ message: 'Права выданы' });
    },
    onError: () => notification.error({ message: 'Не удалось выдать права' }),
  });
}

export function useRevokePermission(folderId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => permissionsApi.revoke(folderId, userId),
    onSuccess: (_res, userId) => {
      qc.setQueryData<PermissionResponse[]>(permissionsKey(folderId), prev =>
        (prev ?? []).filter(p => p.userId !== userId)
      );
      notification.success({ message: 'Права отозваны' });
    },
    onError: () => notification.error({ message: 'Не удалось отозвать права' }),
  });
}
