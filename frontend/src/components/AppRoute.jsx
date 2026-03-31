/**
 * AppRoute — unified route guard component.
 * Handles authentication, feature-flag, and role-based access in one place.
 *
 * Props:
 *   roles    {string[]} optional — allowed roles (omit to allow any authenticated user)
 *   feature  {string}   optional — feature flag key that must be enabled
 *   children {ReactNode}
 */
import { useEffect } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import useAuthStore from '../store/authStore.js';
import FullPageLoader from './ui/FullPageLoader.jsx';

function getFallbackRoute(user, hasFeature) {
  if (user?.role === 'superuser') return '/superuser';
  if (user?.role === 'staff') {
    if (hasFeature('inventory')) return '/inventory';
    if (hasFeature('pos')) return '/sales';
    return '/support';
  }
  return '/dashboard';
}

export default function AppRoute({ roles, feature, children }) {
  const location = useLocation();
  const { isAuthenticated, user, featureFlagsLoaded, hasFeature, refreshFeatureFlags } = useAuthStore();

  useEffect(() => {
    if (isAuthenticated) {
      refreshFeatureFlags();
    }
  }, [isAuthenticated, location.pathname, refreshFeatureFlags]);

  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (!featureFlagsLoaded) return <FullPageLoader />;

  if (roles && (!user || !roles.includes(user.role))) {
    return <Navigate to={getFallbackRoute(user, hasFeature)} replace />;
  }

  if (feature && user?.role !== 'superuser' && !hasFeature(feature)) {
    return <Navigate to={getFallbackRoute(user, hasFeature)} replace />;
  }

  return children;
}
