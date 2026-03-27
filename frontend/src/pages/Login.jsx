/**
 * Login page — full-screen auth page with dark gradient background.
 * Accepts email + password, calls the auth store login function,
 * then redirects to /dashboard or /superuser based on role.
 */
import { useState } from 'react';
import { Navigate, useNavigate, Link } from 'react-router-dom';
import { FiEye, FiEyeOff, FiShield, FiLogIn } from 'react-icons/fi';
import useAuthStore from '../store/authStore.js';
import Alert from '../components/ui/Alert.jsx';
import { apiErrMsg } from '../api/axios.js';
import './Login.css';

export default function Login() {
  const navigate = useNavigate();
  const { login, isAuthenticated, user } = useAuthStore();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPw, setShowPw] = useState(false);
  const [loading, setLoading] = useState(false);
  const [alert, setAlert] = useState(null);

  if (isAuthenticated) {
    if (user?.role === 'superuser') return <Navigate to="/superuser" replace />;
    if (user?.role === 'staff') return <Navigate to="/inventory" replace />;
    return <Navigate to="/dashboard" replace />;
  }

  const showAlert = (message, type = 'error') => setAlert({ message, type });
  const clearAlert = () => setAlert(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    clearAlert();
    setLoading(true);
    try {
      const data = await login(email.trim(), password);
      if (data.user?.role === 'superuser') {
        navigate('/superuser', { replace: true });
      } else if (data.user?.role === 'staff') {
        navigate('/inventory', { replace: true });
      } else {
        navigate('/dashboard', { replace: true });
      }
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-header">
          <div className="login-logo">
            <FiShield size={28} />
          </div>
          <h1 className="login-title">Inventory Avengers</h1>
          <p className="login-subtitle">Sign in to your account</p>
        </div>

        {alert && (
          <Alert message={alert.message} type={alert.type} onClose={clearAlert} />
        )}

        <form onSubmit={handleSubmit} className="login-form">
          <div>
            <label className="form-label">Email Address</label>
            <input
              type="email"
              className="form-control"
              placeholder="you@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoComplete="email"
            />
          </div>
          <div>
            <label className="form-label">Password</label>
            <div className="login-pw-field">
              <input
                type={showPw ? 'text' : 'password'}
                className="form-control"
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                autoComplete="current-password"
              />
              <button
                type="button"
                className="login-pw-toggle"
                onClick={() => setShowPw((v) => !v)}
              >
                {showPw ? <FiEyeOff size={16} /> : <FiEye size={16} />}
              </button>
            </div>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="btn btn-primary login-submit-btn"
          >
            {loading ? (
              <span className="btn-spinner" />
            ) : (
              <FiLogIn size={16} />
            )}
            {loading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>

        <p className="login-footer">
          Don&apos;t have an account?{' '}
          <Link to="/register">Register</Link>
        </p>
      </div>
    </div>
  );
}
