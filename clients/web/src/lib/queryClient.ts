import { QueryCache, QueryClient } from '@tanstack/react-query';
import { notification } from 'antd';

export const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: (error) => {
      const status = (error as { response?: { status?: number } })?.response?.status;
      if (status === 401) return;
      const description =
        (error as { response?: { data?: { title?: string } } })?.response?.data?.title ??
        (error as Error)?.message ??
        'Ошибка сети';
      notification.error({ message: 'Ошибка загрузки данных', description });
    },
  }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});
