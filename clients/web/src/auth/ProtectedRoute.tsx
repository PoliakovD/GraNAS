import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function ProtectedRoute() {
  const { user, loading } = useAuth();
  if (loading) {
    return (
      <div className="auth-loading">
        <div className="spinner" />
      </div>
    );
  }
  return user ? <Outlet /> : <Navigate to="/login" replace />;
}
