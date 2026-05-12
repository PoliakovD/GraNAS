import { screen, fireEvent, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { handlers } from './mocks/handlers';
import { renderWithProviders, makeQueryClient } from './test-utils';
import { RegisterPage } from '../pages/RegisterPage';
import { setAccessToken } from '../api/client';

const server = setupServer(...handlers);
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe('RegisterPage consent', () => {
  beforeEach(() => setAccessToken(null));

  it('renders email consent checkbox checked by default', async () => {
    renderWithProviders(<RegisterPage />, { queryClient: makeQueryClient() });
    const checkbox = await screen.findByRole('checkbox') as HTMLInputElement;
    expect(checkbox.checked).toBe(true);
  });

  it('includes emailNotificationsConsent=false when unchecked', async () => {
    let capturedBody: unknown;
    server.use(
      http.post('http://localhost:8080/api/auth/register', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ userId: 'user-x', message: 'ok' });
      })
    );

    renderWithProviders(<RegisterPage />, { queryClient: makeQueryClient() });

    const checkbox = await screen.findByRole('checkbox');
    fireEvent.click(checkbox);

    const emailInput    = screen.getByPlaceholderText('you@company.com');
    const passwordInput = screen.getByPlaceholderText('Минимум 8 символов');
    fireEvent.change(emailInput,    { target: { value: 'new@test.com' } });
    fireEvent.change(passwordInput, { target: { value: 'ValidPass1' } });

    const submitBtn = screen.getByRole('button', { name: /Зарегистрироваться/i });
    fireEvent.click(submitBtn);

    await waitFor(() => expect(capturedBody).toBeDefined());
    expect((capturedBody as { emailNotificationsConsent: boolean }).emailNotificationsConsent).toBe(false);
  });
});
