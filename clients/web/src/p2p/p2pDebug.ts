// ─────────────────────────────────────────────────────────────────────────────
// TEMPORARY P2P debug log buffer (in-memory pub/sub).
// Used to surface WebRTC/ICE stages in the UI on devices without a console (mobile).
// Self-contained: to remove, delete this file + P2PDebugLog.tsx, the <P2PDebugLog/>
// mount in App.tsx, and the `p2pDebug.log(...)` calls in P2PSession.ts.
// ─────────────────────────────────────────────────────────────────────────────

export type P2PDebugEntry = { t: number; msg: string };

const entries: P2PDebugEntry[] = [];
const listeners = new Set<(e: P2PDebugEntry[]) => void>();
const MAX = 500;

export const p2pDebug = {
  log(msg: string): void {
    entries.push({ t: Date.now(), msg });
    if (entries.length > MAX) entries.shift();
    const snapshot = [...entries];
    listeners.forEach(l => l(snapshot));
    // Mirror to console too (harmless where a console exists).
    console.debug('[P2P]', msg);
  },
  clear(): void {
    entries.length = 0;
    listeners.forEach(l => l([]));
  },
  snapshot(): P2PDebugEntry[] {
    return [...entries];
  },
  subscribe(l: (e: P2PDebugEntry[]) => void): () => void {
    listeners.add(l);
    l([...entries]);
    return () => { listeners.delete(l); };
  },
};
