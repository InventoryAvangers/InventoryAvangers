import { useState, useEffect, useRef, useCallback } from 'react';
import { FiPlus, FiSearch, FiEdit2, FiTrash2, FiBarChart2, FiDownload } from 'react-icons/fi';
import JsBarcode from 'jsbarcode';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Modal from '../components/ui/Modal.jsx';
import Alert from '../components/ui/Alert.jsx';
import Card from '../components/ui/Card.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import { apiGet, apiPost, apiPut, apiDelete, apiErrMsg } from '../api/axios.js';
import useAuthStore from '../store/authStore.js';
import { formatCurrency } from '../utils/helpers.js';

import './Inventory.css';

const EMPTY_FORM = {
  name: '', category: '', costPrice: '', sellingPrice: '',
  quantity: '', threshold: '10', sku: '', barcode: '', barcodeType: 'CODE128',
};

export default function Inventory() {
  const { user, checkRole } = useAuthStore();
  const currency = user?.currency || 'INR';
  const fmt = (v) => formatCurrency(v, currency);
  const isManager = checkRole('owner', 'manager');
  const isOwner = checkRole('owner');

  const [inventoryRecords, setInventoryRecords] = useState([]);

  const [loading, setLoading] = useState(true);
  const [alert, setAlert] = useState(null);
  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const svgRef = useRef(null);

  const showAlert = (message, type = 'error') => setAlert({ message, type });
  const clearAlert = () => setAlert(null);

  const loadInventory = useCallback(async () => {
    try {
      const raw = await apiGet('/inventory');
      setInventoryRecords(Array.isArray(raw) ? raw : (raw.data || []));
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadInventory(); }, [loadInventory]);

  const filtered = inventoryRecords.filter((rec) => {
    const name = rec.productId?.name || '';
    const category = rec.productId?.category || '';
    const matchSearch = name.toLowerCase().includes(search.toLowerCase()) ||
      category.toLowerCase().includes(search.toLowerCase());
    const matchCat = !categoryFilter || category === categoryFilter;
    return matchSearch && matchCat;
  });

  const categories = [...new Set(inventoryRecords.map((rec) => rec.productId?.category).filter(Boolean))].sort();

  // Summary cards
  const totalValue = inventoryRecords.reduce((s, rec) => s + (rec.productId?.costPrice || 0) * rec.quantity, 0);
  const totalProfit = inventoryRecords.reduce((s, rec) => s + ((rec.productId?.sellingPrice || 0) - (rec.productId?.costPrice || 0)) * rec.quantity, 0);
  const lowCount = inventoryRecords.filter((rec) => rec.quantity <= rec.threshold).length;

  const openModal = (rec = null) => {
    const product = rec?.productId;
    setEditingId(product?._id || null);
    setForm(product ? {
      name: product.name || '',
      category: product.category || '',
      costPrice: product.costPrice ?? '',
      sellingPrice: product.sellingPrice ?? '',
      quantity: rec.quantity ?? '',
      threshold: rec.threshold ?? 10,
      sku: product.sku || '',
      barcode: product.barcode || '',
      barcodeType: product.barcodeType || 'CODE128',
    } : EMPTY_FORM);
    setModalOpen(true);
  };

  const closeModal = () => { setModalOpen(false); setEditingId(null); };

  const handleFormChange = (e) => {
    const { name, value } = e.target;
    setForm((f) => ({ ...f, [name]: value }));
  };

  // Barcode rendering
  useEffect(() => {
    if (!svgRef.current || !form.barcode) return;
    try {
      JsBarcode(svgRef.current, form.barcode, {
        format: 'CODE128', width: 2, height: 50, displayValue: true, fontSize: 11,
      });
    } catch {
      if (svgRef.current) svgRef.current.innerHTML = '';
    }
  }, [form.barcode, modalOpen]);

  const generateBarcode = () => {
    let sku = form.sku.trim();
    if (!sku) {
      const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
      sku = 'SKU-';
      for (let i = 0; i < 6; i++) sku += chars[Math.floor(Math.random() * chars.length)];
    }
    setForm((f) => ({ ...f, sku, barcode: sku }));
  };

  const downloadBarcode = () => {
    if (!svgRef.current || !svgRef.current.innerHTML) return showAlert('Generate a barcode first.', 'warning');
    const svgStr = new XMLSerializer().serializeToString(svgRef.current);
    const blob = new Blob([svgStr], { type: 'image/svg+xml' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${form.sku || 'barcode'}.svg`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    const body = {
      name: form.name.trim(),
      category: form.category.trim(),
      costPrice: parseFloat(form.costPrice),
      sellingPrice: parseFloat(form.sellingPrice),
      quantity: parseInt(form.quantity),
      threshold: parseInt(form.threshold) || 10,
      sku: form.sku.trim() || undefined,
      barcode: form.barcode.trim() || undefined,
      barcodeType: 'CODE128',
      // storeId is injected server-side from the auth token
    };
    try {
      if (editingId) {
        await apiPut(`/products/${editingId}`, body);
        showAlert('Product updated successfully.', 'success');
      } else {
        await apiPost('/products', body);
        showAlert('Product added successfully.', 'success');
      }
      closeModal();
      await loadInventory();
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (rec) => {
    const product = rec.productId;
    if (!product?._id) return;
    if (!confirm(`Delete "${product.name}"?\n\nManagers will submit an approval request. Owners will delete immediately.`)) return;
    try {
      const res = await apiDelete(`/products/${product._id}`);
      if (res.approval) {
        showAlert('Deletion request submitted for owner approval.', 'warning');
      } else {
        showAlert(`"${product.name}" deleted successfully.`, 'success');
      }
      await loadInventory();
    } catch (err) {
      showAlert(apiErrMsg(err));
    }
  };

  return (
    <DashboardLayout>
      {alert && <Alert message={alert.message} type={alert.type} onClose={clearAlert} />}

      {/* Summary cards */}
      <div className="inventory-summary-grid">
        <Card title="Total Products" value={inventoryRecords.length} color="indigo" />
        <Card title="Low Stock" value={lowCount} color="amber" />
        <Card title="Inventory Value" value={fmt(totalValue)} color="blue" />
        <Card title="Potential Profit" value={fmt(totalProfit)} color="emerald" />
      </div>

      {/* Toolbar */}
      <div className="card inventory-toolbar">
        <div className="inventory-toolbar-left">
          <div className="inventory-search-wrap">
            <FiSearch className="inventory-search-icon" size={15} />
            <input
              className="form-control"
              placeholder="Search products..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <select
            className="form-control inventory-category-select"
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
          >
            <option value="">All Categories</option>
            {categories.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>

        </div>
        {isManager && (
          <button onClick={() => openModal()} className="btn btn-primary">
            <FiPlus size={15} /> Add Product
          </button>
        )}
      </div>

      {/* Table */}
      <div className="card inventory-table-card">
        {loading ? (
          <LoadingSpinner text="Loading inventory..." />
        ) : (
          <div className="table-wrapper">
            <table className="table">
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Category</th>
                  <th>SKU</th>
                  <th>Cost</th>
                  <th>Price</th>
                  <th>Profit</th>
                  {isOwner && <th>Store</th>}
                  <th>Qty</th>
                  <th>Threshold</th>
                  <th>Status</th>
                  {isManager && <th>Actions</th>}
                </tr>
              </thead>
              <tbody>
                {filtered.length === 0 ? (
                  <tr>
                    <td colSpan={isManager ? (isOwner ? 11 : 10) : (isOwner ? 10 : 9)} className="inventory-empty-cell">
                      No inventory records found
                    </td>
                  </tr>
                ) : (
                  filtered.map((rec) => {
                    const p = rec.productId || {};
                    const isLow = rec.quantity <= rec.threshold;
                    const profit = (p.sellingPrice || 0) - (p.costPrice || 0);
                    return (
                      <tr key={rec._id} className={isLow ? 'inventory-row-low' : ''}>
                        <td className="inventory-col-name">{p.name || '—'}</td>
                        <td><span className="badge badge-gray">{p.category || '—'}</span></td>
                        <td className="inventory-col-sku">{p.sku || '—'}</td>
                        <td>{fmt(p.costPrice)}</td>
                        <td>{fmt(p.sellingPrice)}</td>
                        <td className="inventory-col-profit">{fmt(profit)}</td>
                        {isOwner && (
                          <td className="inventory-col-store">{rec.storeId?.name || '—'}</td>
                        )}
                        <td className={isLow ? 'inventory-col-qty-low' : 'inventory-col-qty-ok'}>{rec.quantity}</td>
                        <td>{rec.threshold}</td>
                        <td>
                          {rec.quantity === 0
                            ? <span className="badge badge-danger">Out of Stock</span>
                            : isLow
                              ? <span className="badge badge-warning">Low Stock</span>
                              : <span className="badge badge-success">In Stock</span>
                          }
                        </td>
                        {isManager && (
                          <td>
                            <div className="inventory-action-btns">
                              <button
                                onClick={() => openModal(rec)}
                                className="btn btn-outline btn-sm"
                              >
                                <FiEdit2 size={12} />
                              </button>
                              <button
                                onClick={() => handleDelete(rec)}
                                className="btn btn-danger btn-sm"
                              >
                                <FiTrash2 size={12} />
                              </button>
                            </div>
                          </td>
                        )}
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Product Modal */}
      <Modal
        isOpen={modalOpen}
        onClose={closeModal}
        title={editingId ? 'Edit Product' : 'Add Product'}
        size="lg"
      >
        <form onSubmit={handleSubmit} className="inventory-modal-form">
          <div className="inventory-modal-grid">
            <div className="inventory-modal-col-full">
              <label className="form-label">Product Name *</label>
              <input name="name" className="form-control" required value={form.name} onChange={handleFormChange} />
            </div>
            <div className="inventory-modal-col-half-sm">
              <label className="form-label">Category *</label>
              <input name="category" className="form-control" required value={form.category} onChange={handleFormChange} list="category-list" />
              <datalist id="category-list">
                {categories.map((c) => <option key={c} value={c} />)}
              </datalist>
            </div>
            <div className="inventory-modal-col-half-sm">
              <label className="form-label">SKU</label>
              <input name="sku" className="form-control" value={form.sku} onChange={handleFormChange} placeholder="Auto-generated if empty" />
            </div>
            <div>
              <label className="form-label">Cost Price *</label>
              <input name="costPrice" type="number" step="0.01" min="0" className="form-control" required value={form.costPrice} onChange={handleFormChange} />
            </div>
            <div>
              <label className="form-label">Selling Price *</label>
              <input name="sellingPrice" type="number" step="0.01" min="0" className="form-control" required value={form.sellingPrice} onChange={handleFormChange} />
            </div>
            <div>
              <label className="form-label">Quantity *</label>
              <input name="quantity" type="number" min="0" className="form-control" required value={form.quantity} onChange={handleFormChange} />
            </div>
            <div>
              <label className="form-label">Low Stock Threshold</label>
              <input name="threshold" type="number" min="0" className="form-control" value={form.threshold} onChange={handleFormChange} />
            </div>

            {/* Barcode */}
            <div className="inventory-modal-col-full">
              <label className="form-label">Barcode</label>
              <div className="inventory-barcode-row">
                <input
                  name="barcode"
                  className="form-control"
                  value={form.barcode}
                  onChange={handleFormChange}
                  placeholder="Scan, type or generate"
                />
                <button type="button" onClick={generateBarcode} className="btn btn-outline btn-sm">
                  <FiBarChart2 size={13} /> Generate
                </button>
                <button type="button" onClick={downloadBarcode} className="btn btn-outline btn-sm">
                  <FiDownload size={13} />
                </button>
              </div>
              {form.barcode && (
                <div className="inventory-barcode-preview">
                  <svg ref={svgRef} />
                </div>
              )}
            </div>
          </div>

          <div className="inventory-modal-footer">
            <button type="button" onClick={closeModal} className="btn btn-secondary">Cancel</button>
            <button type="submit" disabled={saving} className="btn btn-primary">
              {saving && <span className="btn-spinner" />}
              {saving ? 'Saving...' : 'Save Product'}
            </button>
          </div>
        </form>
      </Modal>
    </DashboardLayout>
  );
}
