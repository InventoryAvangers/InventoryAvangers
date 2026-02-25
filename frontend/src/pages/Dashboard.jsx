/**
 * Dashboard page — overview of today's key metrics.
 * Shows KPI cards (revenue, orders, products, low-stock alerts),
 * a 7-day revenue line chart, top products, low-stock items, and recent sales.
 */
import { useState, useEffect, useCallback } from 'react';
import {
  FiDollarSign, FiShoppingBag, FiPackage, FiAlertTriangle,
} from 'react-icons/fi';
import {
  Chart as ChartJS, CategoryScale, LinearScale, PointElement,
  LineElement, Title, Tooltip, Legend, Filler,
} from 'chart.js';
import { Line } from 'react-chartjs-2';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Card from '../components/ui/Card.jsx';
import { apiGet } from '../api/axios.js';
import useAuthStore from '../store/authStore.js';
import { fmtShortDate, getLast7Days, getDayKey, formatCurrency } from '../utils/helpers.js';

import './Dashboard.css';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler);

export default function Dashboard() {
  const user = useAuthStore((s) => s.user);
  const currency = user?.currency || 'INR';
  const fmt = (v) => formatCurrency(v, currency);

  const [sales, setSales] = useState([]);
  const [products, setProducts] = useState([]);
  const [dashStats, setDashStats] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    Promise.all([
      apiGet('/reports/sales'),
      apiGet('/products'),
      apiGet('/reports/dashboard'),
    ]).then(([reportData, prods, stats]) => {
      setSales(reportData.sales || []);
      setProducts(Array.isArray(prods) ? prods : (prods.data || []));
      setDashStats(stats);
    }).catch(console.error).finally(() => setLoading(false));
  }, []);

  const today = new Date().toDateString();
  const todaySales = sales.filter((s) => new Date(s.createdAt).toDateString() === today);
  const todayRevenue = todaySales.reduce(
    (sum, s) => sum + s.totalAmount - (s.returnedAmount || 0),
    0
  );
  const lowCount = products.filter((p) => p.quantity <= p.threshold).length;

  const days = getLast7Days();
  const labels = days.map(getDayKey);
  const revenueMap = Object.fromEntries(labels.map((l) => [l, 0]));
  sales.forEach((s) => {
    const key = getDayKey(s.createdAt);
    if (revenueMap[key] !== undefined) revenueMap[key] += (s.totalAmount - (s.returnedAmount || 0));
  });

  const chartData = {
    labels,
    datasets: [{
      label: `Revenue (${currency})`,
      data: labels.map((l) => revenueMap[l]),
      fill: true,
      backgroundColor: 'rgba(79,70,229,0.08)',
      borderColor: '#4f46e5',
      borderWidth: 2.5,
      pointBackgroundColor: '#4f46e5',
      pointRadius: 4,
      tension: 0.4,
    }],
  };

  const chartOptions = {
    responsive: true,
    plugins: {
      legend: { display: false },
      tooltip: { callbacks: { label: (c) => ' ' + fmt(c.parsed.y) } },
    },
    scales: {
      y: { beginAtZero: true, grid: { color: '#f1f5f9' }, ticks: { callback: (v) => fmt(v) } },
      x: { grid: { display: false } },
    },
  };

  const productMap = {};
  sales.forEach((s) => {
    s.items?.forEach((item) => {
      if (!productMap[item.name]) productMap[item.name] = { qty: 0, revenue: 0 };
      productMap[item.name].qty += item.qty;
      productMap[item.name].revenue += item.price * item.qty;
    });
  });
  const topProducts = Object.entries(productMap)
    .sort((a, b) => b[1].revenue - a[1].revenue)
    .slice(0, 5);

  const lowStockProducts = products.filter((p) => p.quantity <= p.threshold).slice(0, 8);
  const recentSales = sales.slice(0, 10);

  return (
    <DashboardLayout>
      {/* KPI Cards */}
      <div className="dashboard-kpi-grid">
        <Card
          title="Today's Revenue"
          value={fmt(todayRevenue)}
          icon={<FiDollarSign size={18} />}
          color="indigo"
        >
          <p className="dashboard-kpi-note">Net of returns</p>
        </Card>
        <Card
          title="Today's Orders"
          value={todaySales.length}
          icon={<FiShoppingBag size={18} />}
          color="emerald"
        />
        <Card
          title="Total Products"
          value={products.length}
          icon={<FiPackage size={18} />}
          color="blue"
        />
        <Card
          title="Low Stock Alerts"
          value={lowCount}
          icon={<FiAlertTriangle size={18} />}
          color="amber"
        />
      </div>

      {/* Chart + Top Products */}
      <div className="dashboard-mid-grid">
        <div className="card dashboard-chart-card">
          <h3 className="dashboard-card-title">Revenue — Last 7 Days</h3>
          <Line data={chartData} options={chartOptions} />
        </div>

        <div className="card dashboard-top-products-card">
          <h3 className="dashboard-card-title">Top Products</h3>
          {topProducts.length === 0 ? (
            <p className="dashboard-empty-text">No sales data</p>
          ) : (
            <table className="dashboard-table-inner">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Product</th>
                  <th>Qty</th>
                  <th>Revenue</th>
                </tr>
              </thead>
              <tbody>
                {topProducts.map(([name, d], i) => (
                  <tr key={name}>
                    <td className="dashboard-table-rank">{i + 1}</td>
                    <td className="dashboard-table-product-name">{name}</td>
                    <td className="dashboard-table-qty">{d.qty}</td>
                    <td className="dashboard-table-revenue">{fmt(d.revenue)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Low Stock + Recent Sales */}
      <div className="dashboard-bottom-grid">
        <div className="card dashboard-bottom-card">
          <h3 className="dashboard-card-title">Low Stock Items</h3>
          {lowStockProducts.length === 0 ? (
            <p className="dashboard-all-stocked">✓ All stocked up!</p>
          ) : (
            <table className="dashboard-table-inner">
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Category</th>
                  <th>Stock</th>
                  <th>Threshold</th>
                </tr>
              </thead>
              <tbody>
                {lowStockProducts.map((p) => (
                  <tr key={p._id}>
                    <td className="dashboard-lowstock-name">{p.name}</td>
                    <td>
                      <span className="badge badge-gray">{p.category}</span>
                    </td>
                    <td>
                      <span className={`badge ${p.quantity === 0 ? 'badge-danger' : 'badge-warning'}`}>
                        {p.quantity} left
                      </span>
                    </td>
                    <td className="dashboard-table-qty">{p.threshold}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div className="card dashboard-bottom-card">
          <h3 className="dashboard-card-title">Recent Sales</h3>
          {recentSales.length === 0 ? (
            <p className="dashboard-empty-text">No recent sales</p>
          ) : (
            <div>
              {recentSales.map((s) => (
                <div key={s._id} className="dashboard-recent-sale-item">
                  <div>
                    <div className="dashboard-recent-sale-name">
                      {s.customerName || 'Walk-in'}
                    </div>
                    <div className="dashboard-recent-sale-meta">
                      #{s._id.slice(-8).toUpperCase()} · {s.items?.length} item(s) · {fmtShortDate(s.createdAt)}
                    </div>
                  </div>
                  <div className="dashboard-recent-sale-right">
                    <div className="dashboard-recent-sale-amount">{fmt(s.totalAmount)}</div>
                    <span className={`badge ${s.paymentMethod === 'cash' ? 'badge-success' : 'badge-info'}`}>
                      {s.paymentMethod}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </DashboardLayout>
  );
}
