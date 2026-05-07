import { useEffect, useState } from 'react';
import { subscribeToast } from './useToast';
import { Icon } from './Icon';

interface ToastMsg { id: number; text: string }

export function ToastContainer() {
  const [msgs, setMsgs] = useState<ToastMsg[]>([]);

  useEffect(() => {
    return subscribeToast(text => {
      const id = Date.now();
      setMsgs(prev => [...prev, { id, text }]);
      setTimeout(() => setMsgs(prev => prev.filter(m => m.id !== id)), 3200);
    });
  }, []);

  if (!msgs.length) return null;

  return (
    <div className="toast-container">
      {msgs.map(m => (
        <div key={m.id} className="toast">
          <Icon name="check" size={14} />
          {m.text}
        </div>
      ))}
    </div>
  );
}
