import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { GrantPermissionForm } from '../features/permissions/GrantPermissionForm';
import { renderWithProviders } from './test-utils';

describe('GrantPermissionForm', () => {
  it('renders the grant form', () => {
    renderWithProviders(<GrantPermissionForm folderId="folder-1" />);
    expect(screen.getByPlaceholderText(/email/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /выдать/i })).toBeInTheDocument();
  });

  it('submits grant request', async () => {
    renderWithProviders(<GrantPermissionForm folderId="folder-1" />);
    const user = userEvent.setup();

    await user.type(screen.getByPlaceholderText(/email/i), 'other@test.com');
    await user.click(screen.getByRole('button', { name: /выдать/i }));

    // MSW returns success — no error message shown
    await waitFor(() => {
      expect(screen.queryByText(/ошибка/i)).not.toBeInTheDocument();
    });
  });
});
