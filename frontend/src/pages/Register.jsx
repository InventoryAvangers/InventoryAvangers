/**
 * Register page — new user registration form.
 * Fetches available stores, validates password strength,
 * and submits a registration request pending admin approval.
 */
import { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { FiEye, FiEyeOff, FiShield, FiUserPlus } from 'react-icons/fi';
import useAuthStore from '../store/authStore.js';
import Alert from '../components/ui/Alert.jsx';
import axios from 'axios';
import { apiErrMsg } from '../api/axios.js';
import './Register.css';

const PASSWORD_REGEX = /^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/;

export default function Register() {
  const navigate = useNavigate();
  const { register, isAuthenticated } = useAuthStore();

  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [storeId, setStoreId] = useState('');
  const [stores, setStores] = useState([]);
  const [storesLoading, setStoresLoading] = useState(true);
  const [showPw, setShowPw] = useState(false);
  const [loading, setLoading] = useState(false);
  const [alert, setAlert] = useState(null);
  const [registered, setRegistered] = useState(false);

  useEffect(() => {
    const base = import.meta.env.VITE_API_URL || '/api';
    axios.get(`${base}/stores/public`)
      .then((res) => setStores(res.data || []))
      .catch(() => setStores([]))
      .finally(() => setStoresLoading(false));
  }, []);

  if (isAuthenticated) {
    navigate('/dashboard', { replace: true });
    return null;
  }

  const showAlert = (message, type = 'error') => setAlert({ message, type });
  const clearAlert = () => setAlert(null);

  const getPasswordStrength = () => {
    if (!password) return null;
    const checks = [
      password.length >= 8,
      /[A-Z]/.test(password),
      /\d/.test(password),
      /[^A-Za-z0-9]/.test(password),
    ];
    const passed = checks.filter(Boolean).length;
    if (passed <= 1) return { label: 'Weak', modifier: 'weak' };
    if (passed === 2) return { label: 'Fair', modifier: 'fair' };
    if (passed === 3) return { label: 'Good', modifier: 'good' };
    return { label: 'Strong', modifier: 'strong' };
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    clearAlert();

    if (!storeId) {
      return showAlert('Please select a store to register for.');
    }

    if (!PASSWORD_REGEX.test(password)) {
      return showAlert(
        'Password must be at least 8 characters with one uppercase letter, one number, and one special character.'
      );
    }

    setLoading(true);
    try {
      await register(name.trim(), email.trim(), password, storeId);
      setRegistered(true);
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  };

  const strength = getPasswordStrength();

  if (registered) {
    return (
      <div className="register-page">
        <div className="register-card" style={{ textAlign: 'center' }}>
          <div className="register-success-icon">
            <FiUserPlus size={28} />
          </div>
          <h2 className="register-success-title">Registration Submitted!</h2>
          <p className="register-success-text">
            Your registration is pending approval by your store administrator. You&apos;ll be able to log in once approved.
          </p>
          <Link to="/login" className="btn btn-primary" style={{ display: 'inline-flex', justifyContent: 'center', width: '100%' }}>
            Back to Login
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="register-page">
      <div className="register-card">
        <div className="register-header">
          <div className="register-logo">
            <FiShield size={28} />
          </div>
          <h1 className="register-title">Create Account</h1>
          <p className="register-subtitle">Register to request access</p>
        </div>

        {alert && (
          <Alert message={alert.message} type={alert.type} onClose={clearAlert} />
        )}

        <form onSubmit={handleSubmit} className="register-form">
          <div>
            <label className="form-label">Full Name</label>
            <input
              type="text"
              className="form-control"
              placeholder="Jane Smith"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              autoComplete="name"
            />
          </div>
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
            <div className="register-pw-field">
              <input
                type={showPw ? 'text' : 'password'}
                className="form-control"
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                autoComplete="new-password"
              />
              <button
                type="button"
                className="register-pw-toggle"
                onClick={() => setShowPw((v) => !v)}
              >
                {showPw ? <FiEyeOff size={16} /> : <FiEye size={16} />}
              </button>
            </div>
            {strength && (
              <p className={`register-pw-strength register-pw-strength--${strength.modifier}`}>
                Strength: {strength.label}
              </p>
            )}
            <ul className="register-pw-rules">
              <li className={password.length >= 8 ? 'passed' : ''}>At least 8 characters</li>
              <li className={/[A-Z]/.test(password) ? 'passed' : ''}>One uppercase letter</li>
              <li className={/\d/.test(password) ? 'passed' : ''}>One number</li>
              <li className={/[^A-Za-z0-9]/.test(password) ? 'passed' : ''}>One special character</li>
            </ul>
          </div>

          <div>
            <label className="form-label">Store <span style={{ color: '#ef4444' }}>*</span></label>
            {storesLoading ? (
              <div className="form-control" style={{ color: '#94a3b8', fontSize: '0.875rem' }}>Loading stores…</div>
            ) : stores.length === 0 ? (
              <div className="form-control" style={{ color: '#94a3b8', fontSize: '0.875rem' }}>
                No active stores available. Contact your administrator.
              </div>
            ) : (
              <select
                className="form-control"
                value={storeId}
                onChange={(e) => setStoreId(e.target.value)}
                required
              >
                <option value="">— Select a store —</option>
                {stores.map((s) => (
                  <option key={s._id} value={s._id}>
                    {s.shopName || s.name}
                  </option>
                ))}
              </select>
            )}
          </div>

          <button
            type="submit"
            disabled={loading || storesLoading || stores.length === 0}
            className="btn btn-primary register-submit-btn"
          >
            {loading ? (
              <span className="btn-spinner" />
            ) : (
              <FiUserPlus size={16} />
            )}
            {loading ? 'Registering...' : 'Register'}
          </button>
        </form>

        <p className="register-footer">
          Already have an account?{' '}
          <Link to="/login">Sign In</Link>
        </p>
      </div>
    </div>
  );
}
