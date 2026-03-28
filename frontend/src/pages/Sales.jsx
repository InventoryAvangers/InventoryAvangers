import { useState, useEffect, useCallback } from 'react';
import { FiSearch, FiShoppingCart, FiX, FiPlus, FiMinus, FiTrash2, FiFileText } from 'react-icons/fi';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Modal from '../components/ui/Modal.jsx';
import Alert from '../components/ui/Alert.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import { apiGet, apiPost, apiErrMsg } from '../api/axios.js';
import useAuthStore from '../store/authStore.js';
import { formatCurrency } from '../utils/helpers.js';
import { downloadReceiptPDF } from '../utils/receipt.js';
import './Sales.css';

export default function Sales() {
  const user = useAuthStore((s) => s.user);
  const shopBranding = useAuthStore((s) => s.shopBranding);
  const currency = user?.currency || 'INR';
  const fmt = (v) => formatCurrency(v, currency);

  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [cart, setCart] = useState([]);
  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('cash');
  const [processing, setProcessing] = useState(false);
  const [alert, setAlert] = useState(null);
  const [lastSale, setLastSale] = useState(null);
  const [successModalOpen, setSuccessModalOpen] = useState(false);
  const [receiptModalOpen, setReceiptModalOpen] = useState(false);

  const showAlert = (message, type = 'error') => setAlert({ message, type });
  const clearAlert = () => setAlert(null);

  const loadProducts = useCallback(async () => {
    try {
      const data = await apiGet('/products');
      setProducts(Array.isArray(data) ? data : (data.data || []));
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadProducts();
  }, [loadProducts]);

  const categories = [...new Set(products.map((p) => p.category))].sort();

  const filteredProducts = products.filter((p) => {
    const matchSearch = p.name.toLowerCase().includes(search.toLowerCase()) ||
      p.category.toLowerCase().includes(search.toLowerCase()) ||
      (p.sku && p.sku.toLowerCase().includes(search.toLowerCase()));
    const matchCat = !categoryFilter || p.category === categoryFilter;
    return matchSearch && matchCat;
  });

  const cartTotal = cart.reduce((sum, item) => sum + item.price * item.qty, 0);

  const addToCart = (product) => {
    if (product.quantity === 0) return;
    setCart((prev) => {
      const existing = prev.find((i) => i.productId === product._id);
      if (existing) {
        if (existing.qty >= product.quantity) {
          showAlert(`Only ${product.quantity} units available for "${product.name}".`, 'warning');
          return prev;
        }
        return prev.map((i) =>
          i.productId === product._id ? { ...i, qty: i.qty + 1 } : i
        );
      }
      return [...prev, {
        productId: product._id,
        name: product.name,
        sku: product.sku || '',
        price: product.sellingPrice,
        qty: 1,
        maxQty: product.quantity,
      }];
    });
  };

  const changeQty = (productId, delta) => {
    const product = products.find((p) => p._id === productId);
    setCart((prev) =>
      prev
        .map((item) => {
          if (item.productId !== productId) return item;
          const newQty = item.qty + delta;
          if (newQty < 1) return null;
          if (newQty > (product?.quantity ?? item.maxQty)) {
            showAlert(`Max available: ${product?.quantity ?? item.maxQty}`, 'warning');
            return item;
          }
          return { ...item, qty: newQty };
        })
        .filter(Boolean)
    );
  };

  const removeFromCart = (productId) => {
    setCart((prev) => prev.filter((i) => i.productId !== productId));
  };

  const clearCart = () => {
    if (cart.length && confirm('Clear all items from cart?')) setCart([]);
  };

  const checkout = async () => {
    if (!cart.length) return;
    setProcessing(true);
    try {
      const sale = await apiPost('/sales', {
        items: cart.map((item) => ({
          productId: item.productId,
          name: item.name,
          sku: item.sku || '',
          qty: item.qty,
          price: item.price,
        })),
        totalAmount: cartTotal,
        paymentMethod,
        customerName: customerName.trim() || 'Walk-in',
        // storeId is injected server-side from the auth token
      });
      setLastSale(sale);
      setCart([]);
      setCustomerName('');
      setSuccessModalOpen(true);
      await loadProducts();
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setProcessing(false);
    }
  };

  // Receipt PDF — uses shared utility
  const handleDownloadReceiptPDF = () => {
    downloadReceiptPDF(lastSale, shopBranding, fmt);
  };

  return (
    <DashboardLayout>
      {alert && <Alert message={alert.message} type={alert.type} onClose={clearAlert} />}

      <div className="sales-layout">
        {/* Left: Product grid */}
        <div className="sales-products-panel">
          {/* Filters */}
          <div className="sales-filters">
            <div className="sales-search-wrap">
              <FiSearch className="sales-search-icon" size={15} />
              <input
                className="form-control"
                placeholder="Search products..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <select className="form-control sales-category-select" value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value)}>
              <option value="">All</option>
              {categories.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </div>

          {/* Product grid */}
          {loading ? (
            <LoadingSpinner text="Loading products..." />
          ) : (
            <div className="sales-product-grid">
              {filteredProducts.length === 0 ? (
                <div className="sales-empty-products">No products found</div>
              ) : (
                filteredProducts.map((p) => {
                  const outOfStock = p.quantity === 0;
                  const lowStock = p.quantity > 0 && p.quantity <= p.threshold;
                  return (
                    <div
                      key={p._id}
                      onClick={() => !outOfStock && addToCart(p)}
                      className={`product-card ${outOfStock ? 'out-of-stock' : ''}`}
                    >
                      <div className="sales-product-name">{p.name}</div>
                      <div className="sales-product-category">{p.category}</div>
                      <div className="sales-product-price">{fmt(p.sellingPrice)}</div>
                      <div className={`sales-product-stock ${outOfStock ? 'sales-product-stock--out' : lowStock ? 'sales-product-stock--low' : 'sales-product-stock--ok'}`}>
                        {outOfStock ? 'Out of Stock' : `${p.quantity} in stock`}
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          )}
        </div>

        {/* Right: Cart */}
        <div className="card sales-cart">
          <div className="sales-cart-header">
            <div className="sales-cart-title">
              <FiShoppingCart size={16} /> Cart ({cart.length})
            </div>
            {cart.length > 0 && (
              <button onClick={clearCart} className="sales-cart-clear-btn">
                <FiTrash2 size={13} />
              </button>
            )}
          </div>

          {/* Cart items */}
          <div className="sales-cart-items">
            {cart.length === 0 ? (
              <div className="sales-cart-empty">
                <FiShoppingCart size={36} className="sales-cart-empty-icon" />
                <p className="sales-cart-empty-title">Cart is empty</p>
                <p className="sales-cart-empty-hint">Click products to add them</p>
              </div>
            ) : (
              cart.map((item) => (
                <div key={item.productId} className="sales-cart-item">
                  <div className="sales-cart-item-info">
                    <div className="sales-cart-item-name">{item.name}</div>
                    <div className="sales-cart-item-price">{fmt(item.price)} each</div>
                  </div>
                  <div className="sales-cart-qty-controls">
                    <button
                      onClick={() => changeQty(item.productId, -1)}
                      className="sales-cart-qty-btn"
                    >
                      <FiMinus size={10} />
                    </button>
                    <span className="sales-cart-qty-value">{item.qty}</span>
                    <button
                      onClick={() => changeQty(item.productId, 1)}
                      className="sales-cart-qty-btn"
                    >
                      <FiPlus size={10} />
                    </button>
                  </div>
                  <div className="sales-cart-item-total">
                    {fmt(item.price * item.qty)}
                  </div>
                  <button onClick={() => removeFromCart(item.productId)} className="sales-cart-remove-btn">
                    <FiX size={14} />
                  </button>
                </div>
              ))
            )}
          </div>

          {/* Checkout */}
          <div className="sales-checkout">
            <div className="sales-checkout-subtotal">
              <span className="sales-checkout-subtotal-label">Subtotal</span>
              <span className="sales-checkout-subtotal-value">{fmt(cartTotal)}</span>
            </div>
            <div className="sales-checkout-total">
              <span>Total</span>
              <span className="sales-checkout-total-amount">{fmt(cartTotal)}</span>
            </div>

            <input
              className="form-control"
              placeholder="Customer name (optional)"
              value={customerName}
              onChange={(e) => setCustomerName(e.target.value)}
            />
            <select
              className="form-control"
              value={paymentMethod}
              onChange={(e) => setPaymentMethod(e.target.value)}
            >
              <option value="cash">Cash</option>
              <option value="card">Card</option>
              <option value="upi">UPI</option>
            </select>

            <button
              onClick={checkout}
              disabled={!cart.length || processing}
              className="btn btn-primary sales-checkout-btn"
            >
              {processing ? <span className="btn-spinner" /> : null}
              {processing ? 'Processing...' : 'Process Sale'}
            </button>
          </div>
        </div>
      </div>

      {/* Success Modal */}
      <Modal isOpen={successModalOpen} onClose={() => setSuccessModalOpen(false)} title="Sale Complete!">
        <div className="sales-success-content">
          <div className="sales-success-icon">
            <span>✓</span>
          </div>
          {lastSale && (
            <div className="sales-success-info">
              <p className="sales-success-customer">Customer: <strong>{lastSale.customerName || 'Walk-in'}</strong></p>
              <p className="sales-success-amount">{fmt(lastSale.totalAmount)}</p>
              <p className="sales-success-payment">Payment: {lastSale.paymentMethod?.toUpperCase()}</p>
            </div>
          )}
          <div className="sales-success-actions">
            <button onClick={() => setReceiptModalOpen(true)} className="btn btn-outline">
              <FiFileText size={15} /> View Receipt
            </button>
            <button onClick={handleDownloadReceiptPDF} className="btn btn-outline">
              Download PDF
            </button>
            <button onClick={() => setSuccessModalOpen(false)} className="btn btn-primary">
              New Sale
            </button>
          </div>
        </div>
      </Modal>

      {/* Receipt Modal */}
      <Modal isOpen={receiptModalOpen} onClose={() => setReceiptModalOpen(false)} title="Receipt Preview">
        {lastSale && (
          <div className="sales-receipt">
            <div className="sales-receipt-header">
              <div className="sales-receipt-shop-name">Inventory Avengers</div>
              {(shopBranding?.shopName || shopBranding?.name) && (
                <div className="sales-receipt-store-name">{shopBranding.shopName || shopBranding.name}</div>
              )}
              {shopBranding?.address && <div className="sales-receipt-address">{shopBranding.address}</div>}
              <div className="sales-receipt-id">Receipt #{lastSale.receiptNumber || lastSale._id}</div>
              <div className="sales-receipt-date">{new Date(lastSale.createdAt || Date.now()).toLocaleString()}</div>
            </div>
            <div className="sales-receipt-meta"><strong>Customer:</strong> {lastSale.customerName || 'Walk-in'}</div>
            <div className="sales-receipt-meta"><strong>Payment:</strong> {lastSale.paymentMethod?.toUpperCase()}</div>
            <table className="sales-receipt-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Qty</th>
                  <th>Price</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {lastSale.items?.map((it, i) => (
                  <tr key={i}>
                    <td>{it.name}</td>
                    <td>{it.qty}</td>
                    <td>{fmt(it.price)}</td>
                    <td>{fmt(it.price * it.qty)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="sales-receipt-totals">
              <div className="sales-receipt-total-row"><span>Subtotal</span><span>{fmt(lastSale.subtotal || lastSale.totalAmount)}</span></div>
              <div className="sales-receipt-total-row"><span>Tax</span><span>{fmt(lastSale.tax || 0)}</span></div>
              <div className="sales-receipt-grand-total">
                <span>Total</span><span>{fmt(lastSale.totalAmount)}</span>
              </div>
            </div>
            <div className="sales-receipt-thank-you">Thank you for your purchase!</div>
            {shopBranding?.receiptFooter && (
              <div className="sales-receipt-footer-msg">{shopBranding.receiptFooter}</div>
            )}
            <div className="sales-receipt-actions">
              <button onClick={handleDownloadReceiptPDF} className="btn btn-primary sales-checkout-btn">
                Download PDF
              </button>
              <button onClick={() => window.print()} className="btn btn-outline">Print</button>
            </div>
          </div>
        )}
      </Modal>
    </DashboardLayout>
  );
}
