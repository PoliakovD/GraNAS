import { QueryCache, QueryClient } from '@tanstack/react-query';
import { toast } from '../shared/useToast';

const SILENT_404_EXACT = new Set<string>([
  '/api/metadata/folders',
  '/api/share-links',
  '/api/notifications',
  '/api/signaling/devices/folder-devices',
]);
const SILENT_404_PATTERNS: RegExp[] = [
  /^\/api\/sharing\/folders\/[^/]+\/shares$/,
];
const PUBLIC_SHARE_RE = /^\/api\/sharing\/share\/[^/]+$/;

export const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: (error) => {
      const err = error as {
        response?: { status?: number; data?: { title?: string }; config?: { url?: string } };
        config?: { url?: string };
      };
      const status = err.response?.status;
      if (status === 401) return;

      const url = err.response?.config?.url ?? err.config?.url ?? '';
      if (status === 404) {
        if (SILENT_404_EXACT.has(url)) return;
        if (SILENT_404_PATTERNS.some(re => re.test(url))) return;
      }
      if ((status === 404 || status === 410) && PUBLIC_SHARE_RE.test(url)) return;

      const description =
        err.response?.data?.title ??
        (error as Error)?.message ??
        'Ошибка сети';
      toast(`Ошибка загрузки: ${description}`);
    },
  }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});
