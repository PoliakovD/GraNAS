import { useQuery } from '@tanstack/react-query';
import { signalingApi } from '../../api/signaling.api';
import type { FolderDeviceResponse } from '../../types/device';

export function useFolderDevices(folderIds: string[]) {
  const key = folderIds.slice().sort().join(',');
  return useQuery({
    queryKey: ['folder-devices', key],
    queryFn: () => signalingApi.getFolderDevices(folderIds),
    enabled: folderIds.length > 0,
    staleTime: 30_000,
    select: (data): Record<string, FolderDeviceResponse> =>
      Object.fromEntries(data.map(d => [d.folderId, d])),
  });
}
