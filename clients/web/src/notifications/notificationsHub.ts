import {
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { getAccessToken } from '../api/client';
import type { NotificationDto } from '../api/notifications.api';

const BASE_URL = (import.meta.env.VITE_API_BASE_URL as string) ?? 'http://localhost:8080';

let _connection: HubConnection | null = null;
const _notificationCallbacks: Array<(n: NotificationDto) => void> = [];
const _readCallbacks: Array<(id: string) => void> = [];

export function onNotification(cb: (n: NotificationDto) => void) {
  _notificationCallbacks.push(cb);
  return () => {
    const idx = _notificationCallbacks.indexOf(cb);
    if (idx >= 0) _notificationCallbacks.splice(idx, 1);
  };
}

export function onNotificationRead(cb: (id: string) => void) {
  _readCallbacks.push(cb);
  return () => {
    const idx = _readCallbacks.indexOf(cb);
    if (idx >= 0) _readCallbacks.splice(idx, 1);
  };
}

export async function start() {
  if (_connection) return;

  _connection = new HubConnectionBuilder()
    .withUrl(`${BASE_URL}/hubs/notifications`, {
      accessTokenFactory: () => getAccessToken() ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  _connection.on('NotificationReceived', (n: NotificationDto) => {
    _notificationCallbacks.forEach(cb => cb(n));
  });

  _connection.on('NotificationRead', (id: string) => {
    _readCallbacks.forEach(cb => cb(id));
  });

  await _connection.start();
}

export async function stop() {
  if (!_connection) return;
  await _connection.stop();
  _connection = null;
}
