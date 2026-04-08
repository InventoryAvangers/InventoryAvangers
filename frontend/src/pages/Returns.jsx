import { useState, useEffect, useCallback } from 'react';
import { FiSearch, FiRotateCcw, FiCheckSquare, FiSquare } from 'react-icons/fi';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Alert from '../components/ui/Alert.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import { apiGet, apiPost, apiErrMsg } from '../api/axios.js';
import { fmtDate, formatCurrency } from '../utils/helpers.js';
import useAuthStore from '../store/authStore.js';

import './Returns.css';

const REASONS = [
  { value: 'defective',   label: 'Defective (not restocked)' },
  { value: 'wrong_item',  label: 'Wrong Item (restocked)' },
  { value: 'others',      label: 'Others (restocked)' },
];

export default function Returns() {
  const user = useAuthStore((s) => s.user);
  const currency = user?.currency || 'INR';
  const fmt = (v) => formatCurrency(v, currency);

  const [returns, setReturns] = useState([]);
  const [loading, setLoading] = useState(true);
  const [alert, setAlert] = useState(null);
  const [saleIdInput, setSaleIdInput] = useState('');
  const [selectedSale, setSelectedSale] = useState(null);
  const [lookingUp, setLookingUp] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Per-item return state: { [productId]: { checked, qty, reason, maxQty, price } }
  const [itemReturns, setItemReturns] = useState({});

  const showAlert = (message, type = 'error') => setAlert({ message, type });
  const clearAlert = () => setAlert(null);

  const loadReturns = useCallback(async () => {
    try {
      const data = await apiGet('/returns');
      setReturns(data);
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadReturns(); }, [loadReturns]);

  const lookupSale = async () => {
    const id = saleIdInput.trim();
    if (!id) return showAlert('Enter a Sale ID to look up.', 'warning');
    setLookingUp(true);
    try {
      const sales = await apiGet('/sales');
      const found = sales.find(
        (s) => s._id === id || s._id.slice(-8).toUpperCase() === id.toUpperCase()
      );
      if (!found) return showAlert('Sale not found. Check the ID and try again.');
      setSelectedSale(found);
      // Initialize per-item state
      const init = {};
      (found.items || []).forEach((item) => {
        const pid = item.productId?._id || item.productId || '';
        init[pid] = {
          checked: false,
          qty: 1,
          reason: 'defective',
          maxQty: item.qty,
          price: item.price,
          name: item.name,
        };
      });
      setItemReturns(init);
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLookingUp(false);
    }
  };

  const toggleItem = (pid) =>
    setItemReturns((prev) => ({ ...prev, [pid]: { ...prev[pid], checked: !prev[pid].checked } }));

  const updateItem = (pid, field, value) =>
    setItemReturns((prev) => ({ ...prev, [pid]: { ...prev[pid], [field]: value } }));

  const handleReturnSubmit = async (e) => {
    e.preventDefault();
    if (!selectedSale) return showAlert('Look up a sale first.');

    const selected = Object.entries(itemReturns).filter(([, v]) => v.checked);
    if (!selected.length) return showAlert('Select at least one item to return.', 'warning');

    setSubmitting(true);
    let successCount = 0;
    const errors = [];
    for (const [pid, item] of selected) {
      try {
        await apiPost('/returns', {
          saleId: selectedSale._id,
          productId: pid,
          quantity: item.qty,
          reason: item.reason,
          refundAmount: parseFloat((item.price * item.qty).toFixed(2)),
        });
        successCount++;
      } catch (err) {
        errors.push(`${item.name}: ${apiErrMsg(err)}`);
      }
    }

    if (successCount > 0) {
      showAlert(
        `${successCount} item(s) returned successfully.${errors.length ? ' Some errors: ' + errors.join('; ') : ''}`,
        errors.length ? 'warning' : 'success'
      );
    } else {
      showAlert('Failed to process returns: ' + errors.join('; '));
    }

    setSelectedSale(null);
    setSaleIdInput('');
    setItemReturns({});
    await loadReturns();
    setSubmitting(false);
  };

  const selectedCount = Object.values(itemReturns).filter((v) => v.checked).length;
  const selectedRefund = Object.values(itemReturns)
    .filter((v) => v.checked)
    .reduce((sum, v) => sum + v.price * v.qty, 0);

  return (
    <DashboardLayout>
      {alert && <Alert message={alert.message} type={alert.type} onClose={clearAlert} />}

      <div className="returns-layout">
        {/* Return Form */}
        <div className="card returns-form-card">
          <h2 className="returns-form-title">
            <FiRotateCcw size={16} /> Process Return
          </h2>

          {/* Sale lookup */}
          <div className="returns-lookup-group">
            <label className="form-label">Sale ID</label>
            <div className="returns-lookup-row">
              <input
                className="form-control"
                placeholder="Full ID or last 8 chars"
                value={saleIdInput}
                onChange={(e) => setSaleIdInput(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); lookupSale(); } }}
              />
              <button onClick={lookupSale} disabled={lookingUp} className="btn btn-outline">
                <FiSearch size={14} />
                {lookingUp ? '...' : 'Lookup'}
              </button>
            </div>
          </div>

          {selectedSale && (
            <div className="returns-sale-info">
              <strong>Sale #{selectedSale._id.slice(-8).toUpperCase()}</strong> —{' '}
              {selectedSale.customerName || 'Walk-in'} —{' '}
              {fmt(selectedSale.totalAmount)} ({selectedSale.paymentMethod})
            </div>
          )}

          <form onSubmit={handleReturnSubmit} className="returns-form-fields">
            {selectedSale ? (
              <>
                <div style={{ fontSize: '13px', color: '#64748b', marginBottom: 4 }}>
                  Select items to return:
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {selectedSale.items.map((item) => {
                    const pid = item.productId?._id || item.productId || '';
                    const row = itemReturns[pid];
                    if (!row) return null;
                    return (
                      <div
                        key={pid}
                        style={{
                          border: `1px solid ${row.checked ? '#4f46e5' : '#e2e8f0'}`,
                          borderRadius: 8,
                          padding: '10px 12px',
                          background: row.checked ? '#f5f3ff' : '#fff',
                          transition: 'border-color 0.15s',
                        }}
                      >
                        {/* Item header: checkbox + name */}
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: row.checked ? 8 : 0 }}>
                          <button
                            type="button"
                            onClick={() => toggleItem(pid)}
                            style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 0, color: row.checked ? '#4f46e5' : '#94a3b8' }}
                          >
                            {row.checked ? <FiCheckSquare size={18} /> : <FiSquare size={18} />}
                          </button>
                          <div style={{ flex: 1 }}>
                            <div style={{ fontWeight: 600, fontSize: 13 }}>{item.name}</div>
                            <div style={{ fontSize: 11, color: '#94a3b8' }}>
                              Sold: {item.qty} × {fmt(item.price)} = {fmt(item.qty * item.price)}
                            </div>
                          </div>
                        </div>

                        {row.checked && (
                          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, paddingTop: 4 }}>
                            <div>
                              <label className="form-label" style={{ fontSize: 11 }}>Qty to Return</label>
                              <input
                                type="number"
                                className="form-control"
                                min={1}
                                max={row.maxQty}
                                value={row.qty}
                                onChange={(e) => updateItem(pid, 'qty', Math.min(parseInt(e.target.value) || 1, row.maxQty))}
                                style={{ fontSize: 13 }}
                              />
                            </div>
                            <div>
                              <label className="form-label" style={{ fontSize: 11 }}>Reason</label>
                              <select
                                className="form-control"
                                value={row.reason}
                                onChange={(e) => updateItem(pid, 'reason', e.target.value)}
                                style={{ fontSize: 13 }}
                              >
                                {REASONS.map((r) => (
                                  <option key={r.value} value={r.value}>{r.label}</option>
                                ))}
                              </select>
                            </div>
                            <div style={{ gridColumn: '1 / -1', fontSize: 12, color: '#4f46e5', fontWeight: 600 }}>
                              Refund: {fmt(row.price * row.qty)}
                            </div>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>

                {selectedCount > 0 && (
                  <div style={{ background: '#f0fdf4', border: '1px solid #86efac', borderRadius: 8, padding: '10px 12px', fontSize: 13 }}>
                    <strong>{selectedCount} item(s)</strong> selected — Total Refund: <strong>{fmt(selectedRefund)}</strong>
                  </div>
                )}
              </>
            ) : (
              <div style={{ color: '#94a3b8', fontSize: 13, textAlign: 'center', padding: '12px 0' }}>
                Look up a sale to select items for return
              </div>
            )}

            <button
              type="submit"
              disabled={submitting || !selectedSale || selectedCount === 0}
              className="btn btn-primary returns-submit-btn"
            >
              {submitting && <span className="btn-spinner" />}
              {submitting ? 'Processing...' : `Process Return${selectedCount > 0 ? ` (${selectedCount} item${selectedCount > 1 ? 's' : ''})` : ''}`}
            </button>
          </form>
        </div>

        {/* Returns History */}
        <div className="card returns-history-card">
          <div className="returns-history-header">
            <h2 className="returns-history-title">Returns History</h2>
          </div>
          {loading ? (
            <LoadingSpinner text="Loading returns..." />
          ) : (
            <div className="table-wrapper">
              <table className="table">
                <thead>
                  <tr>
                    <th>Return ID</th>
                    <th>Sale ID</th>
                    <th>Product</th>
                    <th>Qty</th>
                    <th>Reason</th>
                    <th>Refund</th>
                    <th>Processed By</th>
                    <th>Date</th>
                  </tr>
                </thead>
                <tbody>
                  {returns.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="returns-empty-cell">No returns recorded</td>
                    </tr>
                  ) : (
                    returns.map((r) => (
                      <tr key={r._id}>
                        <td className="returns-col-id">#{r._id.slice(-8).toUpperCase()}</td>
                        <td className="returns-col-id">
                          #{(r.saleId?._id || r.saleId || '').toString().slice(-8).toUpperCase()}
                        </td>
                        <td>{r.productId?.name || 'N/A'}</td>
                        <td>{r.quantity}</td>
                        <td><span className="badge badge-warning">{r.reason}</span></td>
                        <td className="returns-col-refund">{fmt(r.refundAmount)}</td>
                        <td>{r.processedBy?.name || 'N/A'}</td>
                        <td className="returns-col-date">{fmtDate(r.createdAt)}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </DashboardLayout>
  );
}
