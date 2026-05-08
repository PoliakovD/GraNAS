import { useRef } from 'react';
import { foldersApi } from '../../api/folders.api';

const TOUCH_DEBOUNCE_MS = 5 * 60 * 1000;

export function useTouchFolder() {
  const lastTouched = useRef<Map<string, number>>(new Map());

  return (id: string) => {
    const now = Date.now();
    const last = lastTouched.current.get(id) ?? 0;
    if (now - last < TOUCH_DEBOUNCE_MS) return;
    lastTouched.current.set(id, now);
    foldersApi.touch(id).catch(() => {});
  };
}
