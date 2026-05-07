import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { FolderGrid } from '../features/folders/FolderGrid';
import { renderWithProviders } from './test-utils';
import type { FolderResponse } from '../types/folder';

const MOCK_FOLDERS: FolderResponse[] = [
  { id: 'folder-1', name: 'Root', parentFolderId: null, ownerId: 'user-1', accessLevel: 'Full', path: null, createdAt: '2026-01-01T00:00:00Z', updatedAt: null },
  { id: 'folder-2', name: 'Sub', parentFolderId: 'folder-1', ownerId: 'user-1', accessLevel: 'Full', path: null, createdAt: '2026-01-01T00:00:00Z', updatedAt: null },
];

describe('FolderGrid', () => {
  it('renders folder names', async () => {
    renderWithProviders(
      <FolderGrid
        folders={MOCK_FOLDERS}
        currentUserId="user-1"
        onOpen={() => {}}
        onSelect={() => {}}
        onContext={() => {}}
      />
    );
    await waitFor(() => {
      expect(screen.getByText('Root')).toBeInTheDocument();
      expect(screen.getByText('Sub')).toBeInTheDocument();
    });
  });
});
