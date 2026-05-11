import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ErrorPage, iconFor } from '../shared/ErrorPage';

function renderErrorPage(ui: React.ReactElement) {
  const router = createMemoryRouter([{ path: '/', element: ui }], { initialEntries: ['/'] });
  return render(<RouterProvider router={router} />);
}

describe('iconFor', () => {
  it('maps 403 → shield', () => expect(iconFor(403)).toBe('shield'));
  it('maps 404 → search', () => expect(iconFor(404)).toBe('search'));
  it('maps 410 → link', () => expect(iconFor(410)).toBe('link'));
  it('defaults to circle for unknown codes and "error"', () => {
    expect(iconFor('error')).toBe('circle');
    expect(iconFor(undefined)).toBe('circle');
    expect(iconFor(500)).toBe('circle');
  });
});

describe('ErrorPage', () => {
  it('renders title and description with status suffix', () => {
    renderErrorPage(<ErrorPage code={403} title="Нет доступа" description="Недостаточно прав" />);
    expect(screen.getByText('Нет доступа · 403')).toBeInTheDocument();
    expect(screen.getByText('Недостаточно прав')).toBeInTheDocument();
  });

  it('renders default copy when no props supplied', () => {
    renderErrorPage(<ErrorPage />);
    expect(screen.getByText('Что-то пошло не так')).toBeInTheDocument();
    expect(screen.getByText('Не удалось загрузить страницу')).toBeInTheDocument();
  });

  it('omits status suffix for code="error"', () => {
    renderErrorPage(<ErrorPage code="error" title="Сбой" />);
    expect(screen.getByText('Сбой')).toBeInTheDocument();
    expect(screen.queryByText(/· error/i)).not.toBeInTheDocument();
  });

  it('renders action button and calls onClick', async () => {
    const onClick = vi.fn();
    renderErrorPage(<ErrorPage action={{ label: 'Повторить', onClick }} />);
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});
