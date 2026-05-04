import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, type RenderOptions } from '@testing-library/react';
import { App as AntApp } from 'antd';
import type { ReactNode } from 'react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { setAccessToken } from '../api/client';
import { AuthProvider } from '../auth/AuthContext';

// Fake JWT with sub=user-1 and email=test@test.com, exp far in future
export const FAKE_TOKEN =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyLTEiLCJlbWFpbCI6InRlc3RAdGVzdC5jb20iLCJleHAiOjk5OTk5OTk5OTl9.signature';

export function makeQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

interface WrapperProps {
  children: ReactNode;
  queryClient?: QueryClient;
  initialPath?: string;
}

export function renderWithProviders(
  ui: ReactNode,
  { queryClient = makeQueryClient(), initialPath = '/' }: Omit<WrapperProps, 'children'> = {},
  renderOptions?: RenderOptions,
) {
  setAccessToken(FAKE_TOKEN);

  const router = createMemoryRouter(
    [{ path: '*', element: <>{ui}</> }],
    { initialEntries: [initialPath] },
  );

  function Wrapper() {
    return (
      <QueryClientProvider client={queryClient}>
        <AntApp>
          <AuthProvider>
            <RouterProvider router={router} />
          </AuthProvider>
        </AntApp>
      </QueryClientProvider>
    );
  }

  return render(<Wrapper />, renderOptions);
}
