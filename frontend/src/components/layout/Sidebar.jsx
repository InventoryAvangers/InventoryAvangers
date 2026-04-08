/**
 * Sidebar — fixed left navigation sidebar.
 * Shows shop branding, nav items filtered by role/feature flags, and current user info.
 */
import { NavLink } from 'react-router-dom';
import {
  FiGrid, FiPackage, FiShoppingCart, FiRotateCcw,
  FiBarChart2, FiShield, FiMapPin,
  FiUsers, FiActivity, FiStar, FiCalendar,
} from 'react-icons/fi';
import useAuthStore from '../../store/authStore.js';
import './Sidebar.css';

const NAV_ITEMS = [
  { to: '/dashboard', icon: FiGrid, label: 'Dashboard', roles: ['owner', 'manager'] },
  { to: '/inventory', icon: FiPackage, label: 'Inventory', roles: ['owner', 'manager', 'staff'], feature: 'inventory' },
  { to: '/sales', icon: FiShoppingCart, label: 'Sales / POS', roles: ['owner', 'manager', 'staff'], feature: 'pos' },
  { to: '/returns', icon: FiRotateCcw, label: 'Returns', roles: ['owner', 'manager', 'staff'], feature: 'returns' },
  { to: '/reports', icon: FiBarChart2, label: 'Reports', roles: ['owner', 'manager', 'staff'], feature: 'reports' },
  { to: '/employees', icon: FiUsers, label: 'Employees', roles: ['owner', 'manager'], feature: 'employees' },
  { to: '/vacations', icon: FiCalendar, label: 'Vacations', roles: ['owner', 'manager'] },
  { to: '/audit-log', icon: FiActivity, label: 'Audit Log', roles: ['owner'] },
  { to: '/superuser', icon: FiStar, label: 'Admin Panel', roles: ['superuser'] },
];

export default function Sidebar() {
  const user = useAuthStore((s) => s.user);
  const shopBranding = useAuthStore((s) => s.shopBranding);
  const hasFeature = useAuthStore((s) => s.hasFeature);

  const visibleItems = NAV_ITEMS.filter((item) => {
    if (item.roles && (!user || !item.roles.includes(user.role))) return false;
    if (item.feature && user?.role !== 'superuser' && !hasFeature(item.feature)) return false;
    return true;
  });

  const displayName = user?.displayName || user?.name || '';
  const shopName = shopBranding?.shopName || shopBranding?.name || '';
  const shopLogo = shopBranding?.logoUrl || '';

  return (
    <aside className="sidebar">
      {/* Logo / Shop Branding */}
      <div className="sidebar-branding">
        {shopLogo ? (
          <img src={shopLogo} alt="Shop logo" className="sidebar-logo-img" style={{ width: '2.25rem', height: '2.25rem', borderRadius: '0.5rem', objectFit: 'cover', flexShrink: 0 }} />
        ) : (
          <div className="sidebar-logo-icon">
            <FiShield size={18} />
          </div>
        )}
        <div>
          <div className="sidebar-shop-name">
            {shopName || 'Inventory'}
          </div>
          <div className="sidebar-shop-tagline">
            {shopName ? 'Management' : 'Avengers'}
          </div>
        </div>
      </div>

      {/* Nav */}
      <nav className="sidebar-nav">
        {visibleItems.map(({ to, icon: Icon, label }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              `sidebar-nav-item${isActive ? ' sidebar-nav-item--active' : ''}`
            }
          >
            <Icon size={17} />
            {label}
          </NavLink>
        ))}
      </nav>

      {/* User info */}
      {user && (
        <div className="sidebar-user">
          <div className="sidebar-user-avatar">
            {user.avatar || displayName[0]?.toUpperCase() || 'U'}
          </div>
          <div className="sidebar-user-info">
            <div className="sidebar-user-name">{displayName}</div>
            <div className="sidebar-user-role">{user.role}</div>
          </div>
        </div>
      )}

      {/* Footer */}
      <div className="sidebar-footer">
        <div className="sidebar-footer-text">Inventory Avengers</div>
      </div>
    </aside>
  );
}
