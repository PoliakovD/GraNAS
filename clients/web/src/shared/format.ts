const PALETTE = ['#6938EF', '#00C2A8', '#F79009', '#E5484D', '#2E90FA', '#A653D6', '#0A8954', '#D6336C', '#5A607A'];

export function colorFromString(s: string): string {
  let h = 0;
  for (let i = 0; i < (s || '').length; i++) h = ((h * 31) + s.charCodeAt(i)) >>> 0;
  return PALETTE[h % PALETTE.length];
}

export function initials(s: string): string {
  return (s || '?').trim().slice(0, 1).toUpperCase();
}

export function fmtBytes(b: number | null | undefined): string {
  if (b == null) return '—';
  const u = ['B', 'KB', 'MB', 'GB', 'TB'];
  let i = 0;
  let n = b;
  while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
  return `${n.toFixed(n < 10 && i > 0 ? 1 : 0)} ${u[i]}`;
}

export function relTime(date: string | Date | number | null | undefined): string {
  if (!date) return '—';
  const ms = Date.now() - +new Date(date);
  const m = Math.floor(ms / 60000);
  const h = Math.floor(m / 60);
  const d = Math.floor(h / 24);
  if (m < 1) return 'только что';
  if (m < 60) return `${m} мин назад`;
  if (h < 24) return `${h} ч назад`;
  if (d < 7) return `${d} д назад`;
  return new Date(date).toLocaleDateString('ru-RU');
}
