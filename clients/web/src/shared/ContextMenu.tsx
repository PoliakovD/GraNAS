import { useEffect, useRef } from 'react';
import { Icon } from './Icon';
import type { IconName } from './Icon';

export interface ContextMenuItem {
  icon?: IconName;
  label?: string;
  onClick?: () => void;
  kbd?: string;
  danger?: boolean;
  sep?: boolean;
}

interface Props {
  x: number;
  y: number;
  items: ContextMenuItem[];
  onClose: () => void;
}

export function ContextMenu({ x, y, items, onClose }: Props) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const off = (e: MouseEvent | KeyboardEvent) => {
      if (e instanceof MouseEvent && ref.current?.contains(e.target as Node)) return;
      onClose();
    };
    setTimeout(() => {
      document.addEventListener('mousedown', off);
      document.addEventListener('keydown', off);
    }, 0);
    return () => {
      document.removeEventListener('mousedown', off);
      document.removeEventListener('keydown', off);
    };
  }, [onClose]);

  return (
    <div ref={ref} className="ctx-menu" style={{ left: x, top: y }}>
      {items.map((it, i) =>
        it.sep ? (
          <div key={i} className="ctx-sep" />
        ) : (
          <div
            key={i}
            className={`ctx-item${it.danger ? ' danger' : ''}`}
            onClick={() => { it.onClick?.(); onClose(); }}
          >
            {it.icon && <Icon name={it.icon} size={14} />}
            <span>{it.label}</span>
            {it.kbd && <span className="kbd">{it.kbd}</span>}
          </div>
        )
      )}
    </div>
  );
}
