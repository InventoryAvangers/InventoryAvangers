import { create } from 'zustand';
import { apiPost, apiPut, apiGet } from '../api/axios.js';

const getStoredToken = () => localStorage.getItem('token');
const getStoredUser = () => {
  try {
    const u = localStorage.getItem('user');
    return u ? JSON.parse(u) : null;
  } catch {
    return null;
  }
};
const getStoredFeatureFlags = () => {
  try {
    const f = localStorage.getItem('featureFlags');
    return f ? JSON.parse(f) : null;
  } catch {
    return null;
  }
};
const getStoredShopBranding = () => {
  try {
    const b = localStorage.getItem('shopBranding');
    return b ? JSON.parse(b) : null;
  } catch {
    return null;
  }
};

const getStoredTheme = () => localStorage.getItem('theme') || 'light';
const PRO_FEATURES = {
  inventory: true,
  pos: true,
  returns: true,
  reports: true,
  pdfExport: true,
  employees: true,
  payments: true,
  apiAccess: true,
  darkMode: true,
};

const useAuthStore = create((set, get) => ({
  token: getStoredToken(),
  user: getStoredUser(),
  isAuthenticated: !!getStoredToken(),
  theme: getStoredTheme(),
  featureFlags: getStoredFeatureFlags(),
  shopBranding: getStoredShopBranding(),
  // Always refresh feature access for an authenticated session to avoid stale local storage.
  featureFlagsLoaded: !getStoredToken(),

  refreshFeatureFlags: async () => {
    const { user } = get();

    if (!user) {
      localStorage.removeItem('featureFlags');
      set({ featureFlags: null, featureFlagsLoaded: true });
      return null;
    }

    if (user.role === 'superuser') {
      localStorage.setItem('featureFlags', JSON.stringify(PRO_FEATURES));
      set({ featureFlags: PRO_FEATURES, featureFlagsLoaded: true });
      return PRO_FEATURES;
    }

    set({ featureFlagsLoaded: false });

    try {
      const res = await apiGet('/settings/features');
      const flags = res.data || null;
      localStorage.setItem('featureFlags', JSON.stringify(flags));
      set({ featureFlags: flags, featureFlagsLoaded: true });
      return flags;
    } catch {
      localStorage.removeItem('featureFlags');
      set({ featureFlags: null, featureFlagsLoaded: true });
      return null;
    }
  },

  login: async (email, password) => {
    const data = await apiPost('/auth/login', { email, password });
    localStorage.setItem('token', data.token);
    localStorage.setItem('user', JSON.stringify(data.user));
    set({ token: data.token, user: data.user, isAuthenticated: true, featureFlagsLoaded: false });

    get().refreshFeatureFlags();
    if (data.user?.storeId) {
          // Could not load flags — mark as loaded with no flags; routes will deny access properly

      // Fetch shop branding
      apiGet('/stores')
        .then((stores) => {
          const arr = Array.isArray(stores) ? stores : (stores.data || []);
          const store = arr.find((s) => String(s._id) === String(data.user.storeId)) || arr[0] || null;
          if (store) {
            localStorage.setItem('shopBranding', JSON.stringify(store));
            set({ shopBranding: store });
          }
        })
        .catch(() => {});
    }
    return data;
  },

  register: async (name, email, password, storeId) => {
    const data = await apiPost('/auth/register', { name, email, password, storeId });
    return data;
  },

  logout: () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    localStorage.removeItem('featureFlags');
    localStorage.removeItem('shopBranding');
    set({ token: null, user: null, isAuthenticated: false, featureFlags: null, featureFlagsLoaded: false, shopBranding: null });
  },

  setAuth: (token, user) => {
    localStorage.setItem('token', token);
    localStorage.setItem('user', JSON.stringify(user));
    set({ token, user, isAuthenticated: true });
    // Refresh flags so AppRoute doesn't stay stuck at FullPageLoader
    get().refreshFeatureFlags();
  },

  setShopBranding: (branding) => {
    localStorage.setItem('shopBranding', JSON.stringify(branding));
    set({ shopBranding: branding });
  },

  checkRole: (...roles) => {
    const { user } = get();
    return user && roles.includes(user.role);
  },

  /**
   * Check whether a feature is enabled for the current store.
   * Superusers always have access.
   * Returns false while flags are still loading to prevent premature access;
   * returns true once loaded and the feature is explicitly enabled.
   */
  hasFeature: (featureName) => {
    const { user, featureFlags, featureFlagsLoaded } = get();
    if (user?.role === 'superuser') return true;
    if (!featureFlagsLoaded) return false; // deny until flags are confirmed
    if (!featureFlags) return false;
    return featureFlags[featureName] === true;
  },

  changePassword: async (currentPassword, newPassword) => {
    const data = await apiPut('/auth/change-password', { currentPassword, newPassword });
    const { user } = get();
    if (user) {
      const updatedUser = { ...user, mustChangePassword: false };
      localStorage.setItem('user', JSON.stringify(updatedUser));
      set({ user: updatedUser });
    }
    return data;
  },

  setTheme: (theme) => {
    localStorage.setItem('theme', theme);
    document.documentElement.setAttribute('data-theme', theme);
    set({ theme });
  },
}));

export default useAuthStore;
