/**
 * AuditLog
 *
 * Displays a paginated, filterable table of significant system actions
 * (e.g. user approvals, store changes, role changes). Only accessible to
 * users with the appropriate permissions. Fetches data from the /audit-logs
 * API endpoint and supports client-side filtering by action or actor name.
 */
import { useState, useEffect, useCallback } from 'react';
import { FiActivity, FiChevronLeft, FiChevronRight } from 'react-icons/fi';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Alert from '../components/ui/Alert.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import { apiGet, apiErrMsg } from '../api/axios.js';
import { fmtDate } from '../utils/helpers.js';
import './AuditLog.css';

const ACTION_LABELS = {
  create_store:          'Created Store',
  update_store:          'Updated Store',
  delete_store:          'Deactivated Store',
  assign_store_manager:  'Assigned Store Manager',
  approve_user:          'Approved User',
  reject_user:           'Rejected User',
  promote_employee:      'Promoted Employee',
  demote_employee:       'Demoted Employee',
  transfer_employee:     'Transferred Employee',
  suspend_employee:      'Suspended Employee',
  remove_employee:       'Removed Employee',
  rehire_employee:       'Reinstated Employee',
  change_password:       'Changed Password',
  create_product:        'Added Product',
  update_product:        'Updated Product',
  delete_product:        'Deleted Product',
  create_sale:           'Processed Sale',
  process_return:        'Processed Return',
};

const humanizeMetadata = (action, metadata) => {
  if (!metadata) return null;

  // Action-specific readable formats
  if (action === 'promote_employee' && (metadata.from || metadata.to))
    return `Promoted from ${metadata.from || '?'} to ${metadata.to || '?'}`;
  if (action === 'demote_employee' && (metadata.from || metadata.to))
    return `Demoted from ${metadata.from || '?'} to ${metadata.to || '?'}`;
  if (action === 'transfer_employee')
    return metadata.toStore ? `Transferred to "${metadata.toStore}"` : 'Transferred to new store';
  if (action === 'create_product' && metadata.name)
    return `Added "${metadata.name}" (qty: ${metadata.quantity ?? '?'})`;
  if (action === 'update_product' && metadata.name)
    return `Updated "${metadata.name}"`;
  if (action === 'delete_product' && metadata.name)
    return `Deleted "${metadata.name}"`;
  if (action === 'process_return' && metadata.reason)
    return `Qty: ${metadata.quantity ?? '?'} — Reason: ${metadata.reason}`;
  if (action === 'suspend_employee' && metadata.reason)
    return `Reason: ${metadata.reason}`;
  if (action === 'remove_employee' && metadata.reason)
    return `Reason: ${metadata.reason}`;
  if (action === 'change_password')
    return 'Password updated';
  if (action === 'rehire_employee')
    return 'Employee reinstated';

  // Generic fallback: render all metadata fields as readable pairs
  const pairs = Object.entries(metadata)
    .filter(([, v]) => v !== null && v !== undefined && v !== '')
    .map(([k, v]) => {
      const key = k.replace(/([A-Z])/g, ' $1').replace(/_/g, ' ').trim();
      return `${key.charAt(0).toUpperCase() + key.slice(1)}: ${v}`;
    });
  return pairs.length > 0 ? pairs.join(' · ') : null;
};

const formatDescription = (log) => {
  const actor = log.actorId?.name || 'System';
  const target = log.targetId?.name || '';
  const action = ACTION_LABELS[log.action] || log.action;
  const meta = humanizeMetadata(log.action, log.metadata);

  if (log.action === 'create_store') return `${actor} created a new store`;
  if (log.action === 'update_store') return `${actor} updated store settings`;
  if (log.action === 'approve_user') return `${actor} approved ${target}`;
  if (log.action === 'reject_user') return `${actor} rejected ${target}`;
  if (meta) return `${actor}: ${meta}`;
  if (target) return `${actor} — ${action.toLowerCase()} — ${target}`;
  return `${actor} — ${action.toLowerCase()}`;
};

export default function AuditLog() {
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [alert, setAlert] = useState(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [filter, setFilter] = useState('');

  const showAlert = (message, type = 'error') => setAlert({ message, type });
  const clearAlert = () => setAlert(null);

  const loadLogs = useCallback(async () => {
    setLoading(true);
    try {
      const data = await apiGet('/audit-logs', { page, limit: 25 });
      setLogs(Array.isArray(data) ? data : (data.data || []));
      setTotalPages(data.pages || 1);
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => { loadLogs(); }, [loadLogs]);

  const filtered = filter
    ? logs.filter((l) =>
        l.action?.includes(filter) ||
        l.actorId?.name?.toLowerCase().includes(filter.toLowerCase()) ||
        l.targetId?.name?.toLowerCase().includes(filter.toLowerCase())
      )
    : logs;

  return (
    <DashboardLayout>
      <div className="audit-page-header">
        <div>
          <h2 className="audit-page-title">Audit Log</h2>
          <p className="audit-page-subtitle">Track significant system actions</p>
        </div>
      </div>

      {alert && <Alert message={alert.message} type={alert.type} onClose={clearAlert} />}

      <div className="audit-filter-row">
        <input
          className="form-control audit-filter-input"
          placeholder="Filter by action or user..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
      </div>

      {loading ? (
        <LoadingSpinner />
      ) : filtered.length === 0 ? (
        <div className="card audit-empty-card">
          <FiActivity size={40} className="audit-empty-icon" />
          <p className="audit-empty-text">No audit log entries found.</p>
        </div>
      ) : (
        <>
          <div className="table-wrapper">
            <table className="table">
              <thead>
                <tr>
                  <th>Timestamp</th>
                  <th>Actor</th>
                  <th>Action</th>
                  <th>Description</th>
                  <th>Target</th>
                  <th>Details</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((log) => (
                  <tr key={log._id}>
                    <td className="audit-col-timestamp">{fmtDate(log.createdAt)}</td>
                    <td>
                      {log.actorId ? (
                        <div>
                          <div className="audit-col-actor-name">{log.actorId.name}</div>
                          <div className="audit-col-actor-email">{log.actorId.email}</div>
                        </div>
                      ) : '—'}
                    </td>
                    <td>
                      <span className="badge badge-info">
                        {ACTION_LABELS[log.action] || log.action}
                      </span>
                    </td>
                    <td className="audit-col-description">{formatDescription(log)}</td>
                    <td>
                      {log.targetId ? (
                        <div>
                          <div className="audit-col-target-name">{log.targetId.name}</div>
                          <div className="audit-col-target-email">{log.targetId.email}</div>
                        </div>
                      ) : '—'}
                    </td>
                    <td className="audit-col-details" style={{ fontSize: '11px', color: '#64748b', maxWidth: '220px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {humanizeMetadata(log.action, log.metadata) || '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="audit-pagination">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
                className="btn btn-outline btn-sm"
              >
                <FiChevronLeft size={14} />
              </button>
              <span className="audit-pagination-text">Page {page} of {totalPages}</span>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
                className="btn btn-outline btn-sm"
              >
                <FiChevronRight size={14} />
              </button>
            </div>
          )}
        </>
      )}
    </DashboardLayout>
  );
}
