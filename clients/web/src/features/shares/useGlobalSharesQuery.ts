import { useQuery } from '@tanstack/react-query';
import { sharesApi } from '../../api/shares.api';

export const globalSharesKey = ['shares', 'all'] as const;

// Requires backend GET /api/share-links — see docs/sharing-service-global-listing.md
export function useGlobalSharesQuery() {
  return useQuery({
    queryKey: globalSharesKey,
    queryFn: () => sharesApi.listAll().then(r => r.data),
    retry: false,
  });
}
