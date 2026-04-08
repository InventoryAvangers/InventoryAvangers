/**
 * VacationRequests — Owner/Manager approval page
 * Lists all vacation requests for the store. Owner/manager can approve or decline each with an optional note.
 */
import { useState, useEffect, useCallback } from 'react';
import { FiCalendar, FiCheck, FiX } from 'react-icons/fi';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Alert from '../components/ui/Alert.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import { apiGet, apiPut, apiErrMsg } from '../api/axios.js';
import { fmtDate } from '../utils/helpers.js';

const STATUS_TABS = ['all', 'pending', 'approved', 'declined'];

const STATUS_BADGE = {
  pending:  'badge-warning',
  approved: 'badge-success',
  declined: 'badge-danger',
};

export default function VacationRequests() {
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [alert, setAlert] = useState(null);
  const [tab, setTab] = useState('pending');
  const [reviewingId, setReviewingId] = useState(null);
  const [reviewNote, setReviewNote] = useState('');
  const [processing, setProcessing] = useState(false);

  const showAlert = (msg, type = 'error') => setAlert({ message: msg, type });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await apiGet('/vacations');
      setRequests(data.data || []);
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const filtered = tab === 'all' ? requests : requests.filter((r) => (r.status || r.Status) === tab);

  const handleReview = async (id, action) => {
    setProcessing(true);
    try {
      await apiPut(`/vacations/${id}/${action}`, { note: reviewNote });
      showAlert(`Request ${action}d.`, 'success');
      setReviewingId(null);
      setReviewNote('');
      await load();
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setProcessing(false);
    }
  };

  return (
    <DashboardLayout>
      {alert && <Alert message={alert.message} type={alert.type} onClose={() => setAlert(null)} />}

      <div style={{ marginBottom: 20 }}>
        <h2 style={{ fontSize: '1.25rem', fontWeight: 700, marginBottom: 4 }}>
          <FiCalendar style={{ marginRight: 8 }} />Vacation Requests
        </h2>
        <p style={{ color: '#64748b', fontSize: 13 }}>Review and respond to team vacation requests.</p>
      </div>

      {/* Status tabs */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        {STATUS_TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`btn btn-sm ${tab === t ? 'btn-primary' : 'btn-outline'}`}
            style={{ textTransform: 'capitalize' }}
          >
            {t}
            {t === 'pending' && requests.filter((r) => (r.status || r.Status) === 'pending').length > 0 && (
              <span style={{ marginLeft: 6, background: '#ef4444', color: '#fff', borderRadius: '10px', padding: '1px 6px', fontSize: 11 }}>
                {requests.filter((r) => (r.status || r.Status) === 'pending').length}
              </span>
            )}
          </button>
        ))}
      </div>

      {loading ? <LoadingSpinner /> : filtered.length === 0 ? (
        <div className="card" style={{ padding: 32, textAlign: 'center', color: '#94a3b8' }}>
          No {tab !== 'all' ? tab : ''} vacation requests.
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {filtered.map((r) => {
            const id = r.id || r.Id || r._id;
            const status = r.status || r.Status;
            const emp = r.employee;
            const isReviewing = reviewingId === id;
            return (
              <div key={id} className="card" style={{ padding: '14px 16px' }}>
                <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
                  {/* Avatar */}
                  <div style={{ width: 38, height: 38, borderRadius: '50%', background: '#e0e7ff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700, color: '#4f46e5', flexShrink: 0 }}>
                    {emp?.name?.[0]?.toUpperCase() || '?'}
                  </div>

                  <div style={{ flex: 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 2 }}>
                      <span style={{ fontWeight: 600, fontSize: 14 }}>{emp?.name || 'Unknown'}</span>
                      <span style={{ fontSize: 11, color: '#94a3b8', textTransform: 'capitalize' }}>{emp?.role}</span>
                      <span className={`badge ${STATUS_BADGE[status] || 'badge-gray'}`}>{status}</span>
                    </div>
                    <div style={{ fontSize: 13, color: '#475569', marginBottom: 2 }}>
                      📅 <strong>{fmtDate(r.startDate || r.StartDate)}</strong> → <strong>{fmtDate(r.endDate || r.EndDate)}</strong>
                    </div>
                    {(r.reason || r.Reason) && (
                      <div style={{ fontSize: 12, color: '#64748b' }}>Reason: {r.reason || r.Reason}</div>
                    )}
                    {(r.reviewNote || r.ReviewNote) && (
                      <div style={{ fontSize: 12, color: '#94a3b8', fontStyle: 'italic', marginTop: 2 }}>Review note: {r.reviewNote || r.ReviewNote}</div>
                    )}
                    <div style={{ fontSize: 11, color: '#94a3b8', marginTop: 4 }}>Submitted {fmtDate(r.createdAt || r.CreatedAt)}</div>
                  </div>

                  {status === 'pending' && !isReviewing && (
                    <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
                      <button
                        className="btn btn-sm"
                        style={{ background: '#d1fae5', color: '#065f46', border: 'none' }}
                        onClick={() => { setReviewingId(id); setReviewNote(''); }}
                      >
                        <FiCheck size={13} /> Review
                      </button>
                    </div>
                  )}
                </div>

                {/* Inline review panel */}
                {isReviewing && (
                  <div style={{ marginTop: 12, padding: '12px 14px', background: '#f8fafc', borderRadius: 8, border: '1px solid #e2e8f0' }}>
                    <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8, color: '#374151' }}>Add a note (optional)</div>
                    <textarea
                      className="form-control"
                      rows={2}
                      placeholder="Optional message to the employee..."
                      value={reviewNote}
                      onChange={(e) => setReviewNote(e.target.value)}
                      style={{ resize: 'vertical', marginBottom: 10, fontSize: 13 }}
                    />
                    <div style={{ display: 'flex', gap: 8 }}>
                      <button
                        className="btn btn-sm"
                        style={{ background: '#d1fae5', color: '#065f46', border: 'none' }}
                        onClick={() => handleReview(id, 'approve')}
                        disabled={processing}
                      >
                        <FiCheck size={13} /> Approve
                      </button>
                      <button
                        className="btn btn-sm"
                        style={{ background: '#fee2e2', color: '#991b1b', border: 'none' }}
                        onClick={() => handleReview(id, 'decline')}
                        disabled={processing}
                      >
                        <FiX size={13} /> Decline
                      </button>
                      <button className="btn btn-sm btn-outline" onClick={() => setReviewingId(null)}>Cancel</button>
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </DashboardLayout>
  );
}
