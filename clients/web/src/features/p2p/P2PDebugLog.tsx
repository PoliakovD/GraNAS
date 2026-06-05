// ─────────────────────────────────────────────────────────────────────────────
// TEMPORARY floating P2P debug log panel.
// Shows WebRTC/ICE stages in the UI for devices without a console (mobile).
// To remove: delete this file, the <P2PDebugLog/> mount in App.tsx, p2pDebug.ts,
// and the `p2pDebug.log(...)` calls in P2PSession.ts.
// ─────────────────────────────────────────────────────────────────────────────
import { useEffect, useRef, useState } from 'react';
import { p2pDebug, type P2PDebugEntry } from '../../p2p/p2pDebug';

export function P2PDebugLog() {
  const [open, setOpen] = useState(false);
  const [entries, setEntries] = useState<P2PDebugEntry[]>([]);
  const [copied, setCopied] = useState(false);
  const bodyRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => p2pDebug.subscribe(setEntries), []);

  useEffect(() => {
    if (open && bodyRef.current) bodyRef.current.scrollTop = bodyRef.current.scrollHeight;
  }, [entries, open]);

  const asText = () =>
    entries.map(e => `${new Date(e.t).toLocaleTimeString()}.${String(e.t % 1000).padStart(3, '0')}  ${e.msg}`).join('\n');

  const copy = async () => {
    const text = asText();
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      const ta = document.createElement('textarea');
      ta.value = text;
      document.body.appendChild(ta);
      ta.select();
      document.execCommand('copy');
      document.body.removeChild(ta);
    }
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  const wrap: React.CSSProperties = {
    position: 'fixed', right: 12, bottom: 12, zIndex: 9999,
    fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace', fontSize: 11,
  };
  const btn: React.CSSProperties = {
    background: '#111827', color: '#e5e7eb', border: '1px solid #374151',
    borderRadius: 8, padding: '6px 10px', cursor: 'pointer', fontSize: 12,
  };

  if (!open) {
    return (
      <div style={wrap}>
        <button style={btn} onClick={() => setOpen(true)}>
          🐞 P2P логи{entries.length ? ` (${entries.length})` : ''}
        </button>
      </div>
    );
  }

  return (
    <div style={{ ...wrap, width: 'min(94vw, 460px)' }}>
      <div style={{
        background: '#0b0f17', color: '#e5e7eb', border: '1px solid #374151',
        borderRadius: 10, overflow: 'hidden', boxShadow: '0 8px 30px rgba(0,0,0,.45)',
      }}>
        <div style={{ display: 'flex', gap: 6, alignItems: 'center', padding: '6px 8px', borderBottom: '1px solid #374151', background: '#111827' }}>
          <strong style={{ flex: 1, fontSize: 12 }}>P2P отладка</strong>
          <button style={btn} onClick={() => void copy()}>{copied ? '✓ скопировано' : 'копировать'}</button>
          <button style={btn} onClick={() => p2pDebug.clear()}>очистить</button>
          <button style={btn} onClick={() => setOpen(false)}>скрыть ✕</button>
        </div>
        <div ref={bodyRef} style={{ maxHeight: '46vh', overflowY: 'auto', padding: 8, whiteSpace: 'pre-wrap', lineHeight: 1.45 }}>
          {entries.length === 0
            ? <span style={{ opacity: .6 }}>пока пусто — нажми «Подключиться по P2P»</span>
            : entries.map((e, i) => {
                const err = e.msg.startsWith('✗') || e.msg.startsWith('⚠');
                const ok = e.msg.startsWith('✓');
                return (
                  <div key={i} style={{ color: err ? '#fca5a5' : ok ? '#86efac' : '#cbd5e1' }}>
                    <span style={{ opacity: .5 }}>{new Date(e.t).toLocaleTimeString()} </span>{e.msg}
                  </div>
                );
              })}
        </div>
      </div>
    </div>
  );
}
