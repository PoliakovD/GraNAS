import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Inspector } from '../features/inspector/Inspector';
import { renderWithProviders } from './test-utils';
import type { FolderResponse } from '../types/folder';

const MOCK_FOLDER: FolderResponse = {
  id: 'folder-1', name: 'Root', parentFolderId: null,
  ownerId: 'user-1', accessLevel: 'Full', path: null,
  ownerEmail: null, createdAt: '2026-01-01T00:00:00Z', updatedAt: null, lastAccessedAt: null,
};

describe('Inspector — links tab', () => {
  it('renders links tab', async () => {
    renderWithProviders(
      <Inspector folder={MOCK_FOLDER} isOwner onCreateShare={() => {}} />
    );
    const linksTab = screen.getByRole('button', { name: /ссылки/i });
    linksTab.click();
    await waitFor(() => {
      // The MSW handler returns one non-revoked share link
      expect(screen.getByRole('button', { name: /создать share-ссылку/i })).toBeInTheDocument();
    });
  });

  it('shows active link status', async () => {
    renderWithProviders(
      <Inspector folder={MOCK_FOLDER} isOwner onCreateShare={() => {}} />
    );
    screen.getByRole('button', { name: /ссылки/i }).click();
    await waitFor(() => {
      expect(screen.getByText(/активна/i)).toBeInTheDocument();
    });
  });
});
