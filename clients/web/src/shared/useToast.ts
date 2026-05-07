type Listener = (msg: string) => void;

const listeners: Listener[] = [];

export function toast(msg: string) {
  listeners.forEach(fn => fn(msg));
}

export function subscribeToast(fn: Listener): () => void {
  listeners.push(fn);
  return () => {
    const i = listeners.indexOf(fn);
    if (i >= 0) listeners.splice(i, 1);
  };
}
