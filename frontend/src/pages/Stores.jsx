/**
 * Stores page — lets managers view their assigned store and lets admins
 * browse all store locations. Displays per-store KPI analytics (daily/monthly
 * sales, profit, inventory value, low-stock alerts) and a staff roster with
 * role, status and last-login information.
 */
import { useState, useEffect, useCallback } from 'react';
import {
  FiMapPin, FiUser, FiUsers, FiDollarSign, FiTrendingUp,
  FiShoppingBag, FiShoppingCart, FiPackage, FiAlertTriangle, FiRefreshCw,
} from 'react-icons/fi';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import Alert from '../components/ui/Alert.jsx';
import { apiGet, apiErrMsg } from '../api/axios.js';
import useAuthStore from '../store/authStore.js';
import { fmt, fmtDate } from '../utils/helpers.js';
import './Stores.css';

export default function Stores() {
  const user = useAuthStore((s) => s.user);
  const [stores, setStores] = useState([]);
  const [selectedStore, setSelectedStore] = useState(null);
  const [stats, setStats] = useState(null);
  const [employees, setEmployees] = useState([]);
  const [loadingStores, setLoadingStores] = useState(true);
  const [loadingStats, setLoadingStats] = useState(false);
  const [alert, setAlert] = useState(null);

  const loadStores = useCallback(async () => {
    try {
      const data = await apiGet('/stores');
      const list = Array.isArray(data) ? data : [];
      setStores(list);

      // Managers auto-select their assigned store
      if (user?.role === 'manager' && list.length > 0) {
        setSelectedStore(list[0]);
      }
    } catch (err) {
      setAlert({ message: apiErrMsg(err), type: 'error' });
    } finally {
      setLoadingStores(false);
    }
  }, [user]);

  useEffect(() => {
    loadStores();
  }, [loadStores]);

  const loadStoreDetails = useCallback(async (store) => {
    if (!store) return;
    setLoadingStats(true);
    setStats(null);
    setEmployees([]);
    try {
      const [statsData, empData] = await Promise.all([
        apiGet(`/stores/${store._id}/stats`),
        apiGet('/employees', { storeId: store._id }),
      ]);
      setStats(statsData.data || statsData);
      const empList = empData.data || empData;
      setEmployees(Array.isArray(empList) ? empList : []);
    } catch (err) {
      setAlert({ message: apiErrMsg(err), type: 'error' });
    } finally {
      setLoadingStats(false);
    }
  }, []);

  useEffect(() => {
    if (selectedStore) {
      loadStoreDetails(selectedStore);
    }
  }, [selectedStore, loadStoreDetails]);

  const handleSelectStore = (store) => {
    setSelectedStore(store);
  };

  const statCards = stats ? [
    { label: 'Daily Sales', value: fmt(stats.dailySales), icon: FiDollarSign, iconColor: '#4f46e5', bgColor: '#eef2ff' },
    { label: 'Daily Profit', value: fmt(stats.dailyProfit), icon: FiTrendingUp, iconColor: '#059669', bgColor: '#d1fae5' },
    { label: 'Monthly Sales', value: fmt(stats.monthlySales), icon: FiDollarSign, iconColor: '#2563eb', bgColor: '#dbeafe' },
    { label: 'Monthly Profit', value: fmt(stats.monthlyProfit), icon: FiTrendingUp, iconColor: '#16a34a', bgColor: '#dcfce7' },
    { label: 'Total Sales', value: stats.totalSalesCount, icon: FiShoppingBag, iconColor: '#7c3aed', bgColor: '#ede9fe' },
    { label: 'Monthly Orders', value: stats.monthlySalesCount ?? 0, icon: FiShoppingCart, iconColor: '#0284c7', bgColor: '#e0f2fe' },
    { label: 'Active Staff', value: stats.totalStaff, icon: FiUsers, iconColor: '#d97706', bgColor: '#fef3c7' },
    { label: 'Inventory Value', value: fmt(stats.inventoryValue), icon: FiPackage, iconColor: '#0891b2', bgColor: '#cffafe' },
    { label: 'Low Stock Alerts', value: stats.lowStockCount, icon: FiAlertTriangle, iconColor: '#dc2626', bgColor: '#fee2e2' },
  ] : [];

  return (
    <DashboardLayout>
      <div className="stores-page-header">
        <div>
          <h2 className="stores-page-title">Stores &amp; Analytics</h2>
          <p className="stores-page-subtitle">
            {user?.role === 'manager' ? 'View stats for your assigned store' : 'Select a store to view its analytics'}
          </p>
        </div>
        {selectedStore && (
          <button
            onClick={() => loadStoreDetails(selectedStore)}
            className="btn btn-outline btn-sm"
          >
            <FiRefreshCw size={14} /> Refresh
          </button>
        )}
      </div>

      {alert && (
        <Alert message={alert.message} type={alert.type} onClose={() => setAlert(null)} />
      )}

      <div className="stores-layout">
        {/* Store list */}
        <div>
          <div className="card stores-list-card">
            <h3 className="stores-list-title">
              <FiMapPin size={15} /> Stores
            </h3>
            {loadingStores ? (
              <LoadingSpinner />
            ) : stores.length === 0 ? (
              <p className="stores-no-stores">No stores available</p>
            ) : (
              <div className="stores-list-items">
                {stores.map((store) => (
                  <button
                    key={store._id}
                    onClick={() => handleSelectStore(store)}
                    className={`stores-store-btn${selectedStore?._id === store._id ? ' stores-store-btn--active' : ''}`}
                  >
                    <div className="stores-store-btn-row">
                      <span className="stores-store-btn-name">{store.name}</span>
                      <span
                        className={`badge text-xs ${
                          store.status === 'inactive' ? 'badge-danger' : 'badge-success'
                        }`}
                      >
                        {store.status || 'active'}
                      </span>
                    </div>
                    <div className="stores-store-btn-code">{store.code}</div>
                    <div className="stores-store-btn-manager">
                      <FiUser size={11} />
                      {store.managerId ? store.managerId.name : <em>No manager</em>}
                    </div>
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Stats panel */}
        <div className="stores-stats-panel">
          {!selectedStore ? (
            <div className="card stores-placeholder-card">
              <FiMapPin size={40} className="stores-placeholder-icon" />
              <p className="stores-placeholder-text">Select a store to view its analytics</p>
            </div>
          ) : (
            <>
              <div className="stores-stats-store-header">
                <h3 className="stores-stats-store-name">{selectedStore.name}</h3>
                {selectedStore.address && (
                  <p className="stores-stats-store-address">{selectedStore.address}</p>
                )}
              </div>

              {loadingStats ? (
                <div className="stores-loading-card card">
                  <LoadingSpinner />
                </div>
              ) : stats ? (
                <>
                  {/* KPI Grid */}
                  <div className="stores-kpi-grid">
                    {statCards.map(({ label, value, icon: Icon, iconColor, bgColor }) => (
                      <div key={label} className="card stores-kpi-card">
                        <div className="stores-kpi-icon" style={{ backgroundColor: bgColor }}>
                          <Icon size={16} style={{ color: iconColor }} />
                        </div>
                        <div className="stores-kpi-value">{value}</div>
                        <div className="stores-kpi-label">{label}</div>
                      </div>
                    ))}
                  </div>

                  {/* Employee list */}
                  <div className="card stores-staff-card">
                    <h4 className="stores-staff-title">
                      <FiUsers size={14} /> Staff ({employees.length})
                    </h4>
                    {employees.length === 0 ? (
                      <p className="stores-staff-empty">No staff assigned to this store</p>
                    ) : (
                      <div>
                        <table className="stores-staff-table table">
                          <thead>
                            <tr>
                              <th>Name</th>
                              <th>Role</th>
                              <th>Status</th>
                              <th>Last Login</th>
                            </tr>
                          </thead>
                          <tbody>
                            {employees.map((emp) => (
                              <tr key={emp._id}>
                                <td>
                                  <div className="stores-staff-name">{emp.name}</div>
                                  <div className="stores-staff-email">{emp.email}</div>
                                </td>
                                <td>
                                  <span className="badge badge-info capitalize">{emp.role}</span>
                                </td>
                                <td>
                                  <span
                                    className={`badge ${
                                      emp.status === 'approved'
                                        ? 'badge-success'
                                        : emp.status === 'suspended'
                                        ? 'badge-danger'
                                        : 'badge-warning'
                                    }`}
                                  >
                                    {emp.status}
                                  </span>
                                </td>
                                <td className="stores-staff-last-login">
                                  {emp.lastLogin ? fmtDate(emp.lastLogin) : '—'}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>
                </>
              ) : null}
            </>
          )}
        </div>
      </div>
    </DashboardLayout>
  );
}
