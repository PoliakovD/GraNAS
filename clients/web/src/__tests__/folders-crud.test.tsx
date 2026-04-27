import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { FolderTree } from '../features/folders/FolderTree';
import { renderWithProviders } from './test-utils';

describe('FolderTree', () => {
  it('renders folder names from API', async () => {
    renderWithProviders(<FolderTree />);
    await waitFor(() => {
      expect(screen.getByText('Root')).toBeInTheDocument();
      expect(screen.getByText('Sub')).toBeInTheDocument();
    });
  });
});
