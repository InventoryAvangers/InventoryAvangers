/**
 * OwnerStores page — owner-level management of all store locations. Supports
 * creating new stores, editing existing store details, deactivating stores, and
 * assigning or re-assigning manager accounts to individual store locations.
 * Only accessible to users with the owner role.
 */
import { useState, useEffect, useCallback } from 'react';
import { FiMapPin, FiPlus, FiEdit2, FiTrash2, FiUser, FiUsers, FiSearch } from 'react-icons/fi';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Modal from '../components/ui/Modal.jsx';
import Alert from '../components/ui/Alert.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import { apiGet, apiPost, apiPut, apiDelete, apiErrMsg } from '../api/axios.js';
import './OwnerStores.css';

const EMPTY_FORM = { name: '', code: '', address: '', phone: '', email: '' };

export default function OwnerStores() {
  const [stores, setStores] = useState([]);
  const [managers, setManagers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [alert, setAlert] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [showManagerModal, setShowManagerModal] = useState(false);
  const [editStore, setEditStore] = useState(null);
  const [managerStore, setManagerStore] = useState(null);
  const [selectedManagerId, setSelectedManagerId] = useState('');
  const [form, setForm] = useState(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [search, setSearch] = useState('');

  const showAlert = (message, type = 'error') => setAlert({ message, type });
  const clearAlert = () => setAlert(null);

  const loadStores = useCallback(async () => {
    try {
      const data = await apiGet('/stores');
      setStores(Array.isArray(data) ? data : []);
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  }, []);

  const loadManagers = useCallback(async () => {
    try {
      const data = await apiGet('/employees');
      const list = Array.isArray(data) ? data : (data.data || []);
      setManagers(list.filter((u) => u.role === 'manager'));
    } catch {
      setManagers([]);
    }
  }, []);

  useEffect(() => {
    loadStores();
    loadManagers();
  }, [loadStores, loadManagers]);

  const openCreate = () => {
    setEditStore(null);
    setForm(EMPTY_FORM);
    setShowModal(true);
  };

  const openEdit = (store) => {
    setEditStore(store);
    setForm({
      name: store.name,
      code: store.code,
      address: store.address || '',
      phone: store.phone || '',
      email: store.email || '',
    });
    setShowModal(true);
  };

  const openManagerModal = (store) => {
    setManagerStore(store);
    setSelectedManagerId(store.managerId?._id || '');
    setShowManagerModal(true);
  };

  const handleSave = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editStore) {
        await apiPut(`/stores/${editStore._id}`, form);
        showAlert('Store updated successfully', 'success');
      } else {
        await apiPost('/stores', form);
        showAlert('Store created successfully', 'success');
      }
      setShowModal(false);
      await loadStores();
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (store) => {
    if (!window.confirm(`Deactivate store "${store.name}"?`)) return;
    try {
      await apiDelete(`/stores/${store._id}`);
      showAlert('Store deactivated', 'success');
      await loadStores();
    } catch (err) {
      showAlert(apiErrMsg(err));
    }
  };

  const handleAssignManager = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      await apiPut(`/stores/${managerStore._id}/manager`, { managerId: selectedManagerId || null });
      showAlert('Manager assigned successfully', 'success');
      setShowManagerModal(false);
      await loadStores();
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <DashboardLayout>
      <div className="owner-stores-page-header">
        <div>
          <h2 className="owner-stores-page-title">Stores</h2>
          <p className="owner-stores-page-subtitle">Manage all store locations</p>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
          <div style={{ position: 'relative' }}>
            <FiSearch size={14} style={{ position: 'absolute', left: '0.6rem', top: '50%', transform: 'translateY(-50%)', color: '#94a3b8', pointerEvents: 'none' }} />
            <input
              className="form-control"
              placeholder="Search stores..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ paddingLeft: '2rem', width: '200px' }}
            />
          </div>
          <button onClick={openCreate} className="btn btn-primary">
            <FiPlus size={15} /> New Store
          </button>
        </div>
      </div>

      {alert && <Alert message={alert.message} type={alert.type} onClose={clearAlert} />}

      {loading ? (
        <LoadingSpinner />
      ) : stores.length === 0 ? (
        <div className="card owner-stores-empty-card">
          <FiMapPin size={40} className="owner-stores-empty-icon" />
          <p className="owner-stores-empty-text">No stores yet. Create your first store.</p>
        </div>
      ) : (() => {
        const filtered = stores.filter((s) =>
          !search ||
          s.name?.toLowerCase().includes(search.toLowerCase()) ||
          s.code?.toLowerCase().includes(search.toLowerCase()) ||
          s.address?.toLowerCase().includes(search.toLowerCase())
        );
        return (
          <div className="owner-stores-grid">
            {filtered.length === 0 ? (
              <p style={{ color: '#94a3b8', fontSize: '0.875rem' }}>No stores match your search.</p>
            ) : filtered.map((store) => (
            <div key={store._id} className="card owner-store-card">
              <div className="owner-store-card-header">
                <div>
                  <h3 className="owner-store-card-name">{store.name}</h3>
                  <span className="badge badge-info">{store.code}</span>
                </div>
                <span className={`badge ${store.status === 'inactive' ? 'badge-danger' : 'badge-success'}`}>
                  {store.status || 'active'}
                </span>
              </div>

              {store.address && (
                <p className="owner-store-card-info">{store.address}</p>
              )}
              {store.phone && (
                <p className="owner-store-card-info">{store.phone}</p>
              )}
              {store.email && (
                <p className="owner-store-card-info">{store.email}</p>
              )}

              <div className="owner-store-card-manager">
                <FiUser size={12} />
                {store.managerId ? (
                  <span>{store.managerId.name}</span>
                ) : (
                  <span className="owner-store-card-no-manager">No manager assigned</span>
                )}
              </div>

              <div className="owner-store-card-actions">
                <button onClick={() => openEdit(store)} className="btn btn-outline btn-sm">
                  <FiEdit2 size={12} /> Edit
                </button>
                <button onClick={() => openManagerModal(store)} className="btn btn-outline btn-sm">
                  <FiUsers size={12} /> Manager
                </button>
                <button onClick={() => handleDelete(store)} className="btn btn-danger btn-sm">
                  <FiTrash2 size={12} /> Deactivate
                </button>
              </div>
            </div>
            ))}
          </div>
        );
      })()}

      {/* Create/Edit Modal */}
      <Modal isOpen={showModal} onClose={() => setShowModal(false)} title={editStore ? 'Edit Store' : 'New Store'}>
        <form onSubmit={handleSave} className="owner-stores-form">
          <div>
            <label className="form-label">Store Name *</label>
            <input
              className="form-control"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              required
            />
          </div>
          <div>
            <label className="form-label">Store Code *</label>
            <input
              className="form-control form-control--uppercase"
              value={form.code}
              onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
              placeholder="e.g. MAIN, NYC01"
              required
              disabled={!!editStore}
            />
          </div>
          <div>
            <label className="form-label">Address</label>
            <input
              className="form-control"
              value={form.address}
              onChange={(e) => setForm({ ...form, address: e.target.value })}
            />
          </div>
          <div className="owner-stores-form-grid-2">
            <div>
              <label className="form-label">Phone</label>
              <input
                className="form-control"
                value={form.phone}
                onChange={(e) => setForm({ ...form, phone: e.target.value })}
              />
            </div>
            <div>
              <label className="form-label">Email</label>
              <input
                type="email"
                className="form-control"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
              />
            </div>
          </div>
          <div className="owner-stores-form-footer">
            <button type="button" onClick={() => setShowModal(false)} className="btn btn-outline">
              Cancel
            </button>
            <button type="submit" disabled={saving} className="btn btn-primary">
              {saving ? 'Saving...' : editStore ? 'Update' : 'Create'}
            </button>
          </div>
        </form>
      </Modal>

      {/* Assign Manager Modal */}
      <Modal isOpen={showManagerModal} onClose={() => setShowManagerModal(false)} title="Assign Manager">
        <form onSubmit={handleAssignManager} className="owner-stores-form">
          <p className="owner-stores-assign-intro">
            Assign a manager to <strong>{managerStore?.name}</strong>
          </p>
          <div>
            <label className="form-label">Manager</label>
            <select
              className="form-control"
              value={selectedManagerId}
              onChange={(e) => setSelectedManagerId(e.target.value)}
            >
              <option value="">-- No Manager --</option>
              {managers.map((m) => (
                <option key={m._id} value={m._id}>{m.name} ({m.email})</option>
              ))}
            </select>
          </div>
          <div className="owner-stores-form-footer">
            <button type="button" onClick={() => setShowManagerModal(false)} className="btn btn-outline">
              Cancel
            </button>
            <button type="submit" disabled={saving} className="btn btn-primary">
              {saving ? 'Saving...' : 'Assign'}
            </button>
          </div>
        </form>
      </Modal>
    </DashboardLayout>
  );
}
