import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ShareList } from '../features/shares/ShareList';
import { renderWithProviders } from './test-utils';

describe('ShareList', () => {
  it('fetches and displays share links', async () => {
    renderWithProviders(<ShareList folderId="folder-1" />);
    await waitFor(() => expect(screen.getByText('link-1')).toBeInTheDocument());
  });

  it('shows Активна status for non-revoked links', async () => {
    renderWithProviders(<ShareList folderId="folder-1" />);
    await waitFor(() => expect(screen.getByText('Активна')).toBeInTheDocument());
  });
});
