import axios from 'axios';

const api = axios.create({
  baseURL: '/api',
  timeout: 15000,
});

// ── Request deduplication ──────────────────────────────────────────────────
// If the same GET URL is already in-flight, return the existing Promise
// instead of sending a duplicate network request. This is common when
// components re-render (e.g. StrictMode double-invoke or rapid navigation)
// before the first response arrives.
const pendingGets = new Map();

api.interceptors.request.use((config) => {
  // Attach auth token
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  // Dedup only safe, idempotent GET requests
  if (config.method?.toLowerCase() === 'get') {
    const key = config.baseURL + config.url + JSON.stringify(config.params ?? {});
    if (pendingGets.has(key)) {
      // Cancel this request and return the existing promise via a custom signal
      config._dedupKey = key;
      config._dedupPromise = pendingGets.get(key);
    } else {
      config._dedupKey = key;
    }
  }
  return config;
});

// Response interceptor: handle 401 and provide consistent error messages
api.interceptors.response.use(
  (response) => {
    // Clean up dedup map on success
    if (response.config._dedupKey) {
      pendingGets.delete(response.config._dedupKey);
    }
    return response;
  },
  (error) => {
    // Clean up dedup map on error too
    if (error.config?._dedupKey) {
      pendingGets.delete(error.config._dedupKey);
    }

    if (error.response?.status === 401) {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    if (error.code === 'ECONNABORTED') {
      error.message = 'Request timed out. Please check your connection and try again.';
    } else if (!error.response) {
      error.message = 'Network error. Please check your connection and try again.';
    }
    return Promise.reject(error);
  }
);

/**
 * GET with request deduplication: concurrent calls to the same URL share one in-flight request.
 */
export const apiGet = (url, params) => {
  const key = '/api' + url + JSON.stringify(params ?? {});
  if (pendingGets.has(key)) {
    return pendingGets.get(key);
  }
  const promise = api.get(url, { params }).then((r) => r.data).finally(() => {
    pendingGets.delete(key);
  });
  pendingGets.set(key, promise);
  return promise;
};

export const apiPost   = (url, data)   => api.post(url, data).then((r) => r.data);
export const apiPut    = (url, data)   => api.put(url, data).then((r) => r.data);
export const apiPatch  = (url, data)   => api.patch(url, data).then((r) => r.data);
export const apiDelete = (url)         => api.delete(url).then((r) => r.data);

/** Extract a human-readable error message from an Axios error, with an optional custom fallback */
export const apiErrMsg = (err, fallback = 'An unexpected error occurred.') =>
  err?.response?.data?.message || err?.message || fallback;

export default api;
