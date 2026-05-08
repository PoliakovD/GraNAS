import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { Inspector } from '../features/inspector/Inspector';
import { renderWithProviders } from './test-utils';
import type { FolderResponse } from '../types/folder';

const MOCK_FOLDER: FolderResponse = {
  id: 'folder-1', name: 'Root', parentFolderId: null,
  ownerId: 'user-1', accessLevel: 'Full', path: null,
  ownerEmail: null, createdAt: '2026-01-01T00:00:00Z', updatedAt: null, lastAccessedAt: null,
};

describe('Inspector — people tab', () => {
  it('renders invite form for owner', () => {
    renderWithProviders(
      <Inspector folder={MOCK_FOLDER} isOwner onCreateShare={() => {}} />
    );
    expect(screen.getByPlaceholderText(/email@company\.com/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /выдать/i })).toBeInTheDocument();
  });

  it('submits grant request', async () => {
    renderWithProviders(
      <Inspector folder={MOCK_FOLDER} isOwner onCreateShare={() => {}} />
    );
    const user = userEvent.setup();
    await user.type(screen.getByPlaceholderText(/email@company\.com/i), 'other@test.com');
    await user.click(screen.getByRole('button', { name: /выдать/i }));
    await waitFor(() => {
      expect(screen.queryByText(/ошибка/i)).not.toBeInTheDocument();
    });
  });
});
