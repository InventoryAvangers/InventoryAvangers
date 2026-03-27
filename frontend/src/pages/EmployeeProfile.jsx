/**
 * EmployeeProfile
 *
 * Shows a detailed profile card for a single employee, identified by the
 * :id URL parameter. Displays name, email, role badge, status badge, assigned
 * store, join date, and last login. Fetches data from /employees/:id.
 */
import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { FiArrowLeft, FiUser } from 'react-icons/fi';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Alert from '../components/ui/Alert.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import { RoleBadge } from '../components/ui/Badge.jsx';
import { apiGet, apiErrMsg } from '../api/axios.js';
import { fmtDate } from '../utils/helpers.js';
import './EmployeeProfile.css';

const STATUS_BADGE = {
  approved: 'badge-success',
  pending: 'badge-warning',
  rejected: 'badge-danger',
  suspended: 'badge-danger',
  deactivated: 'badge-gray',
};

export default function EmployeeProfile() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [employee, setEmployee] = useState(null);
  const [loading, setLoading] = useState(true);
  const [alert, setAlert] = useState(null);

  const showAlert = (message, type = 'error') => setAlert({ message, type });

  useEffect(() => {
    apiGet(`/employees/${id}`)
      .then((data) => setEmployee(data.data || data))
      .catch((err) => showAlert(apiErrMsg(err)))
      .finally(() => setLoading(false));
  }, [id]);

  return (
    <DashboardLayout>
      <button
        onClick={() => navigate(-1)}
        className="btn btn-outline btn-sm"
      >
        <FiArrowLeft size={14} /> Back
      </button>

      {alert && <Alert message={alert.message} type={alert.type} />}

      {loading ? (
        <LoadingSpinner />
      ) : !employee ? (
        <p className="ep-not-found">Employee not found.</p>
      ) : (
        <div className="card ep-profile-card">
          <div className="ep-profile-top">
            <div className="ep-avatar">
              {employee.name?.[0]?.toUpperCase() || <FiUser size={24} />}
            </div>
            <div>
              <h2 className="ep-name">{employee.name}</h2>
              <p className="ep-email">{employee.email}</p>
            </div>
          </div>

          <div className="ep-detail-list">
            <div className="ep-detail-row">
              <span className="ep-detail-label">Role</span>
              <RoleBadge role={employee.role} />
            </div>
            <div className="ep-detail-row">
              <span className="ep-detail-label">Status</span>
              <span className={`badge ${STATUS_BADGE[employee.status] || 'badge-gray'}`}>
                {employee.status}
              </span>
            </div>
            <div className="ep-detail-row">
              <span className="ep-detail-label">Store</span>
              <span className="ep-detail-value">
                {employee.storeId ? `${employee.storeId.name} (${employee.storeId.code})` : '—'}
              </span>
            </div>
            <div className="ep-detail-row">
              <span className="ep-detail-label">Joined</span>
              <span className="ep-detail-value">{fmtDate(employee.createdAt)}</span>
            </div>
            {employee.lastLogin && (
              <div className="ep-detail-row">
                <span className="ep-detail-label">Last Login</span>
                <span className="ep-detail-value">{fmtDate(employee.lastLogin)}</span>
              </div>
            )}
          </div>
        </div>
      )}
    </DashboardLayout>
  );
}
