import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { handlers } from './mocks/handlers';
import { renderWithProviders } from './test-utils';
import { AccountTab } from '../pages/settings/AccountTab';

const server = setupServer(...handlers);
beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const BASE = 'http://localhost:8080';

async function waitForAuth() {
  // Wait for auth to initialize (spinner disappears, email appears)
  await waitFor(() => expect(screen.queryByText('Загрузить фото')).toBeInTheDocument(), { timeout: 4000 });
}

describe('AccountTab', () => {
  it('shows email and id from user context', async () => {
    renderWithProviders(<AccountTab />);
    await waitForAuth();
    expect(screen.getByText('test@test.com')).toBeInTheDocument();
    expect(screen.getByText('user-1')).toBeInTheDocument();
  });

  it('shows initials avatar when no avatar uploaded', async () => {
    // Default handler returns 404 for GET /api/auth/me/avatar
    renderWithProviders(<AccountTab />);
    await waitForAuth();

    // initials('test@test.com') = 'T' (uppercase)
    await waitFor(() => expect(screen.getByText('T')).toBeInTheDocument(), { timeout: 4000 });
    expect(screen.queryByRole('img')).toBeNull();
  });

  it('shows <img> when avatar is available', async () => {
    const blob = new Blob([new Uint8Array(4)], { type: 'image/png' });
    server.use(
      http.get(`${BASE}/api/auth/me/avatar`, () =>
        new HttpResponse(blob, {
          headers: { 'Content-Type': 'image/png' },
        }))
    );

    renderWithProviders(<AccountTab />);
    await waitForAuth();
    await waitFor(() => expect(screen.getByRole('img')).toBeInTheDocument(), { timeout: 4000 });
  });

  it('shows upload button', async () => {
    renderWithProviders(<AccountTab />);
    await waitForAuth();
    expect(screen.getByText('Загрузить фото')).toBeInTheDocument();
  });
});
