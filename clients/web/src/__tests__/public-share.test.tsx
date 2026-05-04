import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { PublicSharePage } from '../pages/PublicSharePage';

function renderPublicShare(token: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [{ path: '/s/:token', element: <PublicSharePage /> }],
    { initialEntries: [`/s/${token}`] },
  );
  return render(
    <QueryClientProvider client={qc}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

describe('PublicSharePage', () => {
  it('shows folder name for a valid token', async () => {
    renderPublicShare('valid-token');
    await waitFor(() => expect(screen.getByText('Root')).toBeInTheDocument());
  });

  it('shows revoked message for 410', async () => {
    renderPublicShare('revoked');
    await waitFor(() => expect(screen.getByText(/отозвана/i)).toBeInTheDocument());
  });

  it('shows not found message for 404', async () => {
    renderPublicShare('notfound');
    await waitFor(() => expect(screen.getByText(/не найдена/i)).toBeInTheDocument());
  });
});
