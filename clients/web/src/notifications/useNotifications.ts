import { useEffect } from 'react';
import {
  useQuery,
  useMutation,
  useQueryClient,
  useInfiniteQuery,
} from '@tanstack/react-query';
import {
  getUnreadCount,
  listNotifications,
  markRead,
  markAllRead,
} from '../api/notifications.api';
import { onNotification } from './notificationsHub';

export function useUnreadCount() {
  const queryClient = useQueryClient();

  useEffect(() => {
    const unsub = onNotification(() => {
      queryClient.invalidateQueries({ queryKey: ['notifications', 'unread-count'] });
    });
    return unsub;
  }, [queryClient]);

  return useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: getUnreadCount,
    refetchInterval: 60_000,
  });
}

export function useNotificationsList() {
  return useInfiniteQuery({
    queryKey: ['notifications', 'list'],
    queryFn: ({ pageParam }: { pageParam?: string }) =>
      listNotifications(pageParam, 20),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: last => last.nextCursor ?? undefined,
  });
}

export function useMarkRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: markRead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] });
    },
  });
}

export function useMarkAllRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: markAllRead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] });
    },
  });
}
