import { Spin } from 'antd';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function ProtectedRoute() {
  const { user, loading } = useAuth();
  if (loading) return <Spin fullscreen />;
  return user ? <Outlet /> : <Navigate to="/login" replace />;
}
