import { pushApi } from '../api/push.api';

const SW_URL = '/sw.js';
const LS_KEY = 'push_endpoint';

function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = atob(base64);
  return Uint8Array.from([...rawData].map(c => c.charCodeAt(0)));
}

export async function enablePush(): Promise<void> {
  if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
    throw new Error('Push notifications are not supported in this browser');
  }

  const permission = await Notification.requestPermission();
  if (permission !== 'granted') {
    throw new Error('Push notification permission denied');
  }

  const { publicKey } = await pushApi.vapidKey();
  const reg = await navigator.serviceWorker.register(SW_URL, { scope: '/' });
  await navigator.serviceWorker.ready;

  const subscription = await reg.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToUint8Array(publicKey) as Uint8Array<ArrayBuffer>,
  });

  await pushApi.subscribe(subscription.toJSON() as PushSubscriptionJSON);
  localStorage.setItem(LS_KEY, subscription.endpoint);
}

export async function disablePush(): Promise<void> {
  const endpoint = localStorage.getItem(LS_KEY);

  const reg = await navigator.serviceWorker.getRegistration(SW_URL);
  const sub = await reg?.pushManager.getSubscription();
  if (sub) {
    await sub.unsubscribe();
  }

  if (endpoint) {
    await pushApi.unsubscribe(endpoint);
    localStorage.removeItem(LS_KEY);
  }
}

export function isPushEnabled(): boolean {
  if (typeof Notification === 'undefined') return false;
  return (
    Notification.permission === 'granted' &&
    !!localStorage.getItem(LS_KEY)
  );
}
