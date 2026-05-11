import { isRouteErrorResponse, useRouteError } from 'react-router-dom';
import { Icon, type IconName } from './Icon';

export interface ErrorPageProps {
  code?: number | string;
  title?: string;
  description?: string;
  action?: { label: string; onClick: () => void };
}

export function iconFor(code: number | string | undefined): IconName {
  if (code === 403) return 'shield';
  if (code === 404) return 'search';
  if (code === 410) return 'link';
  return 'circle';
}

export function ErrorPage(props: ErrorPageProps) {
  const routeError = useRouteError();
  let { code, title, description, action } = props;

  if (routeError && code === undefined) {
    if (isRouteErrorResponse(routeError)) {
      code = routeError.status;
      title = title ?? routeError.statusText;
    } else {
      code = 'error';
    }
  }
  title = title ?? 'Что-то пошло не так';
  description = description ?? 'Не удалось загрузить страницу';

  return (
    <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24 }}>
      <div className="empty">
        <div className="glyph"><Icon name={iconFor(code)} size={28} /></div>
        <h4>
          {title}
          {code !== undefined && code !== 'error' ? ` · ${code}` : ''}
        </h4>
        <p>{description}</p>
        {action && (
          <button className="btn primary" onClick={action.onClick} style={{ marginTop: 12 }}>
            {action.label}
          </button>
        )}
      </div>
    </div>
  );
}
