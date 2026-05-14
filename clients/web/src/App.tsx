import { QueryClientProvider } from '@tanstack/react-query';
import { Navigate, RouterProvider, createBrowserRouter } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { ThemeProvider } from './shared/ThemeContext';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { queryClient } from './lib/queryClient';
import { FolderDetailPage } from './pages/FolderDetailPage';
import { FoldersPage } from './pages/FoldersPage';
import { HomePage } from './pages/HomePage';
import { LinksPage } from './pages/LinksPage';
import { LoginPage } from './pages/LoginPage';
import { PublicSharePage } from './pages/PublicSharePage';
import { RecentPage } from './pages/RecentPage';
import { RegisterPage } from './pages/RegisterPage';
import { SettingsPage } from './pages/SettingsPage';
import { SharedPage } from './pages/SharedPage';
import { AppLayout } from './shared/Layout';
import { ErrorBoundary } from './shared/ErrorBoundary';
import { ErrorPage } from './shared/ErrorPage';

const router = createBrowserRouter([
  {
    element: <ErrorBoundary />,
    children: [
      { path: '/login', element: <LoginPage /> },
      { path: '/register', element: <RegisterPage /> },
      { path: '/s/:token', element: <PublicSharePage /> },
      {
        element: <ProtectedRoute />,
        children: [
          {
            element: <AppLayout />,
            errorElement: <ErrorPage />,
            children: [
              { path: '/', element: <HomePage /> },
              { path: '/folders', element: <FoldersPage /> },
              { path: '/folders/:id', element: <FolderDetailPage /> },
              { path: '/shared', element: <SharedPage /> },
              { path: '/links', element: <LinksPage /> },
              { path: '/recent', element: <RecentPage /> },
              { path: '/settings/*', element: <SettingsPage /> },
            ],
          },
        ],
      },
      { path: '*', element: <Navigate to="/" replace /> },
    ],
  },
]);

export default function App() {
  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}
