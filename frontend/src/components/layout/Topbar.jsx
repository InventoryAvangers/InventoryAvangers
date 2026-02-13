/**
 * Topbar — fixed top header bar.
 * Shows the current page title, shop name, role badge, notifications,
 * and a user profile dropdown with settings and logout links.
 */
import { useState, useRef, useEffect } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { FiLogOut, FiSettings, FiChevronDown, FiMessageSquare } from 'react-icons/fi';
import { RoleBadge } from '../ui/Badge.jsx';
import NotificationDropdown from '../NotificationDropdown.jsx';
import useAuthStore from '../../store/authStore.js';
import './Topbar.css';

const PAGE_TITLES = {
  '/dashboard': 'Dashboard',
  '/inventory': 'Inventory',
  '/sales':     'Sales / POS',
  '/returns':   'Returns',
  '/reports':   'Reports',
  '/stores':    'Stores',
  '/employees': 'Employee Management',
  '/audit-log': 'Audit Log',
  '/settings':  'Settings',
  '/superuser': 'Admin Panel',
  '/support':   'Support Messages',
};

export default function Topbar() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout, shopBranding } = useAuthStore();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef(null);

  const handleLogout = () => {
    setDropdownOpen(false);
    logout();
    navigate('/login');
  };

  useEffect(() => {
    const handler = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const pathKey = Object.keys(PAGE_TITLES).find((k) =>
    location.pathname === k || location.pathname.startsWith(k + '/')
  );
  const title = PAGE_TITLES[pathKey] || 'Dashboard';

  const displayName = user?.displayName || user?.name || '';
  const shopName = shopBranding?.shopName || shopBranding?.name || '';

  return (
    <header className="topbar">
      <div className="topbar-left">
        <h1 className="topbar-page-title">{title}</h1>
        {shopName && user?.role !== 'superuser' && (
          <span className="topbar-shop-name">— {shopName}</span>
        )}
      </div>

      <div className="topbar-right">
        {user && <RoleBadge role={user.role} />}
        <NotificationDropdown />

        {/* Profile dropdown */}
        <div className="topbar-dropdown-wrap" ref={dropdownRef}>
          <button
            onClick={() => setDropdownOpen((o) => !o)}
            className="topbar-profile-btn"
            title="Profile menu"
          >
            <div className="topbar-avatar">
              {user?.avatar || displayName[0]?.toUpperCase() || 'U'}
            </div>
            <span className="topbar-display-name">{displayName}</span>
            <FiChevronDown size={14} className="topbar-chevron" />
          </button>

          {dropdownOpen && (
            <div className="topbar-dropdown">
              <div className="topbar-dropdown-user">
                <p className="topbar-dropdown-username">{displayName}</p>
                <p className="topbar-dropdown-email">{user?.email}</p>
              </div>
              <Link
                to="/settings"
                onClick={() => setDropdownOpen(false)}
                className="topbar-dropdown-item"
              >
                <FiSettings size={14} />
                Settings
              </Link>
              {user?.role !== 'superuser' && (
                <Link
                  to="/support"
                  onClick={() => setDropdownOpen(false)}
                  className="topbar-dropdown-item"
                >
                  <FiMessageSquare size={14} />
                  Help / Support
                </Link>
              )}
              <button
                onClick={handleLogout}
                className="topbar-dropdown-item topbar-dropdown-item--danger"
              >
                <FiLogOut size={14} />
                Logout
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
