import { useEffect, useRef, useState } from 'react';
import { createHubConnection } from '../../p2p/signalingClient';
import type { OwnerStatus } from '../../p2p/types';

export function useOwnerOnlineStatus(folderId: string | undefined): OwnerStatus {
  const [status, setStatus] = useState<OwnerStatus>('unknown');
  const hubRef = useRef<ReturnType<typeof createHubConnection> | null>(null);

  useEffect(() => {
    if (!folderId) return;

    const hub = createHubConnection();
    hubRef.current = hub;

    hub.on('OwnerOnlineStatusChanged', (id: string, isOnline: boolean) => {
      if (id === folderId) setStatus(isOnline ? 'online' : 'offline');
    });

    hub.start()
      .then(() => hub.invoke('WatchFolder', folderId))
      .catch(() => setStatus('unknown'));

    return () => {
      void hub.stop();
      hubRef.current = null;
      setStatus('unknown');
    };
  }, [folderId]);

  return status;
}
