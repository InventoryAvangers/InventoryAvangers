import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import AppRoute from './components/AppRoute.jsx';
import Login from './pages/Login.jsx';
import Register from './pages/Register.jsx';
import Landing from './pages/Landing.jsx';
import Dashboard from './pages/Dashboard.jsx';
import Inventory from './pages/Inventory.jsx';
import Sales from './pages/Sales.jsx';
import Returns from './pages/Returns.jsx';
import Reports from './pages/Reports.jsx';
import Stores from './pages/Stores.jsx';
import EmployeeManagement from './pages/EmployeeManagement.jsx';
import EmployeeProfile from './pages/EmployeeProfile.jsx';
import AuditLog from './pages/AuditLog.jsx';
import ForbiddenPage from './pages/ForbiddenPage.jsx';
import NoPermission from './pages/NoPermission.jsx';
import Settings from './pages/Settings.jsx';
import SuperuserPanel from './pages/SuperuserPanel.jsx';
import SupportMessages from './pages/SupportMessages.jsx';
import useAuthStore from './store/authStore.js';

function CatchAll() {
  const { isAuthenticated, user } = useAuthStore();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (user?.role === 'staff') return <Navigate to="/inventory" replace />;
  return <Navigate to={user?.role === 'superuser' ? '/superuser' : '/dashboard'} replace />;
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Public */}
        <Route path="/landing" element={<Landing />} />
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route path="/forbidden" element={<ForbiddenPage />} />
        <Route path="/no-permission" element={<NoPermission />} />
        <Route path="/" element={<Navigate to="/landing" replace />} />

        {/* /approvals redirects to /employees (merged) */}
        <Route path="/approvals" element={<Navigate to="/employees" replace />} />

        {/* Protected — any authenticated user (non-superuser) */}
        <Route path="/dashboard" element={<AppRoute roles={['owner', 'manager']}><Dashboard /></AppRoute>} />
        <Route path="/inventory" element={<AppRoute roles={['owner', 'manager', 'staff']}><Inventory /></AppRoute>} />
        <Route path="/sales" element={<AppRoute roles={['owner', 'manager', 'staff']}><Sales /></AppRoute>} />
        <Route path="/returns" element={<AppRoute roles={['owner', 'manager', 'staff']} feature="returns"><Returns /></AppRoute>} />
        <Route path="/reports" element={<AppRoute roles={['owner', 'manager']} feature="reports"><Reports /></AppRoute>} />
        <Route path="/settings" element={<AppRoute><Settings /></AppRoute>} />
        <Route path="/support" element={<AppRoute><SupportMessages /></AppRoute>} />

        {/* Superuser Panel */}
        <Route path="/superuser" element={<AppRoute roles={['superuser']}><SuperuserPanel /></AppRoute>} />

        {/* Owner + Manager */}
        <Route path="/stores" element={<AppRoute roles={['owner', 'manager']}><Stores /></AppRoute>} />
        <Route path="/audit-log" element={<AppRoute roles={['owner']}><AuditLog /></AppRoute>} />
        <Route path="/employees" element={<AppRoute roles={['owner', 'manager']} feature="employees"><EmployeeManagement /></AppRoute>} />
        <Route path="/employees/:id" element={<AppRoute roles={['owner', 'manager']} feature="employees"><EmployeeProfile /></AppRoute>} />
        <Route path="/user-approvals" element={<Navigate to="/employees" replace />} />

        {/* Catch-all */}
        <Route path="*" element={<CatchAll />} />
      </Routes>
    </BrowserRouter>
  );
}
