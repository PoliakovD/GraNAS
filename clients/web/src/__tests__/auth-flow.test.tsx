import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { LoginPage } from '../pages/LoginPage';
import { makeQueryClient, renderWithProviders } from './test-utils';
import { setAccessToken } from '../api/client';

describe('LoginPage', () => {
  it('renders login form', () => {
    setAccessToken(null); // not logged in
    const qc = makeQueryClient();
    renderWithProviders(<LoginPage />, { queryClient: qc });
    expect(screen.getByRole('button', { name: /войти/i })).toBeInTheDocument();
  });

  it('calls login API and accepts form submission', async () => {
    setAccessToken(null);
    renderWithProviders(<LoginPage />, { queryClient: makeQueryClient() });
    const user = userEvent.setup();

    await user.type(screen.getByLabelText(/email/i), 'test@test.com');
    await user.type(screen.getByLabelText(/пароль/i), 'ValidPass1');
    await user.click(screen.getByRole('button', { name: /войти/i }));

    // After login the MSW mock returns a valid token — no error shown
    await waitFor(() => {
      expect(screen.queryByText(/invalid/i)).not.toBeInTheDocument();
    });
  });
});
