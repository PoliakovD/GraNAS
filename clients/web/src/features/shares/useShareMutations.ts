import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { sharesApi } from '../../api/shares.api';
import { toast } from '../../shared/useToast';
import type { CreateShareRequest } from '../../types/share';

export function sharesKey(folderId: string) {
  return ['shares', folderId] as const;
}

export function useSharesQuery(folderId: string) {
  return useQuery({
    queryKey: sharesKey(folderId),
    queryFn: () => sharesApi.list(folderId).then(r => r.data),
  });
}

export function useCreateShare(folderId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateShareRequest) =>
      sharesApi.create(folderId, data).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: sharesKey(folderId) }),
    onError: () => toast('Не удалось создать ссылку'),
  });
}

export function useRevokeShare(folderId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => sharesApi.revokeById(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: sharesKey(folderId) }),
    onError: () => toast('Не удалось отозвать ссылку'),
  });
}
