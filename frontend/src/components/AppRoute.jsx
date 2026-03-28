/**
 * AppRoute — unified route guard component.
 * Handles authentication, feature-flag, and role-based access in one place.
 *
 * Props:
 *   roles    {string[]} optional — allowed roles (omit to allow any authenticated user)
 *   feature  {string}   optional — feature flag key that must be enabled
 *   children {ReactNode}
 */
import { Navigate } from 'react-router-dom';
import useAuthStore from '../store/authStore.js';
import FullPageLoader from './ui/FullPageLoader.jsx';

export default function AppRoute({ roles, feature, children }) {
  const { isAuthenticated, user, featureFlagsLoaded, hasFeature } = useAuthStore();

  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (!featureFlagsLoaded) return <FullPageLoader />;

  if (roles && (!user || !roles.includes(user.role))) {
    if (user?.role === 'superuser') return <Navigate to="/superuser" replace />;
    if (user?.role === 'staff') return <Navigate to="/inventory" replace />;
    return <Navigate to="/dashboard" replace />;
  }

  if (feature && user?.role !== 'superuser' && !hasFeature(feature)) {
    if (user?.role === 'staff') return <Navigate to="/inventory" replace />;
    return <Navigate to="/dashboard" replace />;
  }

  return children;
}
