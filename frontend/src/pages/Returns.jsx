import { useState, useEffect, useCallback } from 'react';
import { FiSearch, FiRotateCcw } from 'react-icons/fi';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Alert from '../components/ui/Alert.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import { apiGet, apiPost, apiErrMsg } from '../api/axios.js';
import { fmtDate, formatCurrency } from '../utils/helpers.js';
import useAuthStore from '../store/authStore.js';

import './Returns.css';

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

  // Return form state
  const [returnProductId, setReturnProductId] = useState('');
  const [returnQty, setReturnQty] = useState(1);
  const [returnReason, setReturnReason] = useState('defective');
  const [refundAmount, setRefundAmount] = useState('');

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

  // Auto-calculate refund when product/qty changes
  useEffect(() => {
    if (!selectedSale || !returnProductId) return;
    const item = selectedSale.items.find((i) => i.productId === returnProductId || i.productId?._id === returnProductId);
    if (item) {
      setRefundAmount((item.price * returnQty).toFixed(2));
    }
  }, [returnProductId, returnQty, selectedSale]);

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
      // Set default product
      if (found.items?.length) {
        const firstItem = found.items[0];
        setReturnProductId(firstItem.productId?._id || firstItem.productId || '');
        setReturnQty(1);
      }
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLookingUp(false);
    }
  };

  const handleReturnSubmit = async (e) => {
    e.preventDefault();
    if (!selectedSale) return showAlert('Look up a sale first.');
    setSubmitting(true);
    try {
      await apiPost('/returns', {
        saleId: selectedSale._id,
        productId: returnProductId,
        quantity: returnQty,
        reason: returnReason,
        refundAmount: parseFloat(refundAmount),
      });
      showAlert('Return processed successfully. ' + (returnReason === 'defective' ? 'Item NOT restocked (Defective).' : 'Stock has been restocked.'), 'success');
      setSelectedSale(null);
      setSaleIdInput('');
      setReturnProductId('');
      setReturnQty(1);
      setRefundAmount('');
      await loadReturns();
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setSubmitting(false);
    }
  };

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
            <div>
              <label className="form-label">Product</label>
              <select
                className="form-control"
                value={returnProductId}
                onChange={(e) => setReturnProductId(e.target.value)}
                disabled={!selectedSale}
                required
              >
                {!selectedSale ? (
                  <option value="">-- Look up sale first --</option>
                ) : (
                  selectedSale.items.map((item) => {
                    const pid = item.productId?._id || item.productId;
                    return (
                      <option key={pid} value={pid}>
                        {item.name} ({item.qty} × {fmt(item.price)})
                      </option>
                    );
                  })
                )}
              </select>
            </div>

            <div>
              <label className="form-label">Quantity</label>
              <input
                type="number"
                min="1"
                className="form-control"
                value={returnQty}
                onChange={(e) => setReturnQty(parseInt(e.target.value) || 1)}
                required
              />
            </div>

            <div>
              <label className="form-label">Reason</label>
              <select
                className="form-control"
                value={returnReason}
                onChange={(e) => setReturnReason(e.target.value)}
                required
              >
                <option value="defective">Defective (not restocked)</option>
                <option value="wrong_item">Wrong Item (restocked)</option>
                <option value="others">Others (restocked)</option>
              </select>
            </div>

            <div>
              <label className="form-label">Refund Amount</label>
              <input
                type="number"
                step="0.01"
                min="0"
                className="form-control"
                value={refundAmount}
                onChange={(e) => setRefundAmount(e.target.value)}
                required
              />
            </div>

            <button
              type="submit"
              disabled={submitting || !selectedSale}
              className="btn btn-primary returns-submit-btn"
            >
              {submitting && <span className="btn-spinner" />}
              {submitting ? 'Processing...' : 'Process Return'}
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
