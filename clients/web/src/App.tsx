import { QueryClientProvider } from '@tanstack/react-query';
import { App as AntApp, ConfigProvider } from 'antd';
import ruRU from 'antd/locale/ru_RU';
import { Navigate, RouterProvider, createBrowserRouter } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { queryClient } from './lib/queryClient';
import { FolderDetailPage } from './pages/FolderDetailPage';
import { LoginPage } from './pages/LoginPage';
import { MyFoldersPage } from './pages/MyFoldersPage';
import { PublicSharePage } from './pages/PublicSharePage';
import { RegisterPage } from './pages/RegisterPage';
import { SharedWithMePage } from './pages/SharedWithMePage';
import { AppLayout } from './shared/Layout';
import { ErrorBoundary } from './shared/ErrorBoundary';

const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/folders" replace /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  { path: '/s/:token', element: <PublicSharePage /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { path: '/folders', element: <MyFoldersPage /> },
          { path: '/folders/:id', element: <FolderDetailPage /> },
          { path: '/shared', element: <SharedWithMePage /> },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/folders" replace /> },
]);

export default function App() {
  return (
    <ConfigProvider locale={ruRU} theme={{ token: { colorPrimary: '#722ed1' } }}>
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <ErrorBoundary>
              <RouterProvider router={router} />
            </ErrorBoundary>
          </AuthProvider>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  );
}
