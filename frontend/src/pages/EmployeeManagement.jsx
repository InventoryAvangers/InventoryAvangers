/**
 * EmployeeManagement
 *
 * Lists all employees visible to the current user (owners see all stores,
 * managers see their own store). Owners and managers can also approve or
 * reject pending user registration requests. Owners additionally have
 * promote, demote, suspend, and remove actions per employee.
 */
import { useState, useEffect, useCallback } from 'react';
import { FiUsers, FiArrowUp, FiArrowDown, FiSlash, FiTrash2, FiEye, FiCheck, FiX, FiClock } from 'react-icons/fi';
import { Link } from 'react-router-dom';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Alert from '../components/ui/Alert.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import Modal from '../components/ui/Modal.jsx';
import { RoleBadge } from '../components/ui/Badge.jsx';
import { apiGet, apiPut, apiDelete, apiErrMsg } from '../api/axios.js';
import useAuthStore from '../store/authStore.js';
import { fmtDate } from '../utils/helpers.js';
import './EmployeeManagement.css';

const STATUS_BADGE = {
  approved: 'badge-success',
  pending: 'badge-warning',
  rejected: 'badge-danger',
  suspended: 'badge-danger',
  deactivated: 'badge-gray',
};

export default function EmployeeManagement() {
  const { user } = useAuthStore();
  const isOwner = user?.role === 'owner';
  const isManager = user?.role === 'manager';

  const [employees, setEmployees] = useState([]);
  const [pendingUsers, setPendingUsers] = useState([]);
  const [pendingRequests, setPendingRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [alert, setAlert] = useState(null);
  const [approveModal, setApproveModal] = useState(null);
  const [approveRole, setApproveRole] = useState('staff');
  const [saving, setSaving] = useState(false);

  const showAlert = (message, type = 'error') => setAlert({ message, type });
  const clearAlert = () => setAlert(null);

  const loadData = useCallback(async () => {
    try {
      const empData = await apiGet('/employees');
      setEmployees(Array.isArray(empData) ? empData : (empData.data || []));

      if (isOwner || isManager) {
        try {
          const pending = await apiGet('/approvals/pending-users');
          setPendingUsers(Array.isArray(pending) ? pending : (pending.data || []));

          const approvals = await apiGet('/approvals');
          const approvalsArr = Array.isArray(approvals) ? approvals : (approvals.data || []);
          setPendingRequests(approvalsArr.filter(a => a.status === 'pending'));
        } catch { /* non-critical */ }
      }
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  }, [isOwner, isManager]);

  useEffect(() => { loadData(); }, [loadData]);

  const handleAction = async (url, method = 'put', body = {}) => {
    try {
      if (method === 'delete') await apiDelete(url);
      else await apiPut(url, body);
      await loadData();
    } catch (err) {
      showAlert(apiErrMsg(err));
    }
  };

  const handleApprove = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      await apiPut(`/approvals/users/${approveModal._id}/approve`, {
        role: approveRole,
      });
      showAlert('User approved successfully.', 'success');
      setApproveModal(null);
      await loadData();
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setSaving(false);
    }
  };

  const handleReject = async (userId) => {
    if (!window.confirm('Reject this user request?')) return;
    try {
      await apiPut(`/approvals/users/${userId}/reject`);
      showAlert('User rejected.', 'success');
      await loadData();
    } catch (err) {
      showAlert(apiErrMsg(err));
    }
  };

  const handleRequestAction = async (id, status) => {
    if (status === 'rejected' && !window.confirm('Reject this request?')) return;
    try {
      await apiPut(`/approvals/${id}`, { status });
      showAlert(`Request ${status}.`, 'success');
      await loadData();
    } catch (err) {
      showAlert(apiErrMsg(err));
    }
  };

  return (
    <DashboardLayout>
      <div className="emp-mgmt-page-header">
        <div>
          <h2 className="emp-mgmt-page-title">Employee Management</h2>
          <p className="emp-mgmt-page-subtitle">
            {isOwner ? 'All employees across stores' : 'Employees in your store'}
          </p>
        </div>
      </div>

      {alert && <Alert message={alert.message} type={alert.type} onClose={clearAlert} />}

      {loading ? (
        <LoadingSpinner />
      ) : (
        <>
          {/* ── Pending Approvals Section ── */}
          {(isOwner || isManager) && pendingUsers.length > 0 && (
            <div className="emp-mgmt-pending-section">
              <div className="emp-mgmt-pending-header">
                <span className="emp-mgmt-pending-icon"><FiClock size={16} /></span>
                <h3 className="emp-mgmt-pending-title">
                  Pending Approvals
                  <span className="emp-mgmt-pending-count">
                    {pendingUsers.length}
                  </span>
                </h3>
              </div>
              <div className="table-wrapper">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Email</th>
                      <th>Requested</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pendingUsers.map((u) => (
                      <tr key={u._id}>
                        <td>{u.name}</td>
                        <td>{u.email}</td>
                        <td>{fmtDate(u.createdAt)}</td>
                        <td>
                          <div className="emp-mgmt-action-btns">
                            <button
                              onClick={() => { setApproveModal(u); setApproveRole('staff'); }}
                              className="btn btn-success btn-sm"
                            >
                              <FiCheck size={12} /> Approve
                            </button>
                            <button
                              onClick={() => handleReject(u._id)}
                              className="btn btn-danger btn-sm"
                            >
                              <FiX size={12} /> Reject
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* ── Pending Requests Section (Owner Approvals) ── */}
          {isOwner && pendingRequests.length > 0 && (
            <div className="emp-mgmt-pending-section" style={{ marginTop: '2rem' }}>
              <div className="emp-mgmt-pending-header">
                <span className="emp-mgmt-pending-icon"><FiClock size={16} /></span>
                <h3 className="emp-mgmt-pending-title">
                  Pending Action Requests
                  <span className="emp-mgmt-pending-count">
                    {pendingRequests.length}
                  </span>
                </h3>
              </div>
              <div className="table-wrapper">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Action</th>
                      <th>Description</th>
                      <th>Requested</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pendingRequests.map((req) => (
                      <tr key={req._id}>
                        <td><span className="badge badge-warning" style={{textTransform:'capitalize'}}>{req.action.replace(/_/g, ' ')}</span></td>
                        <td>{req.description}</td>
                        <td>{fmtDate(req.createdAt)}</td>
                        <td>
                          <div className="emp-mgmt-action-btns">
                            <button
                              onClick={() => handleRequestAction(req._id, 'approved')}
                              className="btn btn-success btn-sm"
                            >
                              <FiCheck size={12} /> Approve
                            </button>
                            <button
                              onClick={() => handleRequestAction(req._id, 'rejected')}
                              className="btn btn-danger btn-sm"
                            >
                              <FiX size={12} /> Reject
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* ── Active Employees ── */}
          {employees.length === 0 ? (
            <div className="card emp-mgmt-empty-card">
              <FiUsers size={40} className="emp-mgmt-empty-icon" />
              <p className="emp-mgmt-empty-text">No employees found.</p>
            </div>
          ) : (
            <div className="table-wrapper">
              <table className="table">
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Email</th>
                    <th>Role</th>
                    <th>Store</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {employees.map((emp) => (
                    <tr key={emp._id}>
                      <td>{emp.name}</td>
                      <td>{emp.email}</td>
                      <td><RoleBadge role={emp.role} /></td>
                      <td>{emp.storeId?.name || '—'}</td>
                      <td>
                        <span className={`badge ${STATUS_BADGE[emp.status] || 'badge-gray'}`}>
                          {emp.status}
                        </span>
                      </td>
                      <td>
                        <div className="emp-mgmt-action-btns">
                          <Link to={`/employees/${emp._id}`} className="btn btn-outline btn-sm" title="View profile">
                            <FiEye size={12} />
                          </Link>
                          {emp.role === 'staff' && isOwner && (
                            <button onClick={() => handleAction(`/employees/${emp._id}/promote`)} className="btn btn-success btn-sm" title="Promote to manager">
                              <FiArrowUp size={12} />
                            </button>
                          )}
                          {emp.role === 'manager' && isOwner && (
                            <button onClick={() => handleAction(`/employees/${emp._id}/demote`)} className="btn btn-warning btn-sm" title="Demote to staff">
                              <FiArrowDown size={12} />
                            </button>
                          )}
                          {isOwner && (
                            <>
                              {emp.status !== 'suspended' && (
                                <button onClick={() => handleAction(`/employees/${emp._id}/suspend`)} className="btn btn-warning btn-sm" title="Suspend">
                                  <FiSlash size={12} />
                                </button>
                              )}
                              <button
                                onClick={() => { if (window.confirm(`Remove employee "${emp.name}"?`)) handleAction(`/employees/${emp._id}`, 'delete'); }}
                                className="btn btn-danger btn-sm"
                                title="Remove"
                              >
                                <FiTrash2 size={12} />
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {/* Approve Modal */}
      {approveModal && (
        <Modal isOpen={true} onClose={() => setApproveModal(null)} title={`Approve ${approveModal.name}`}>
          <form onSubmit={handleApprove} className="emp-mgmt-approve-modal-form">
            <div>
              <label className="form-label">Assign Role</label>
              <select className="form-control" value={approveRole} onChange={(e) => setApproveRole(e.target.value)}>
                <option value="staff">Staff</option>
                <option value="manager">Manager</option>
              </select>
            </div>
            <div className="emp-mgmt-approve-modal-footer">
              <button type="button" className="btn btn-secondary" onClick={() => setApproveModal(null)}>Cancel</button>
              <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Approving...' : 'Approve'}</button>
            </div>
          </form>
        </Modal>
      )}
    </DashboardLayout>
  );
}
