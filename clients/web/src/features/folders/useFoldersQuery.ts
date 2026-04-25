import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { notification } from 'antd';
import { foldersApi } from '../../api/folders.api';
import type { CreateFolderRequest } from '../../types/folder';

export const FOLDERS_KEY = ['folders'] as const;

export function useFoldersQuery() {
  return useQuery({ queryKey: FOLDERS_KEY, queryFn: () => foldersApi.list().then(r => r.data) });
}

export function useCreateFolder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateFolderRequest) => foldersApi.create(data).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: FOLDERS_KEY }),
    onError: () => notification.error({ message: 'Не удалось создать папку' }),
  });
}

export function useDeleteFolder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => foldersApi.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: FOLDERS_KEY }),
    onError: () => notification.error({ message: 'Не удалось удалить папку' }),
  });
}
