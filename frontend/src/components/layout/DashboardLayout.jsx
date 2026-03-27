/**
 * DashboardLayout — main app layout wrapper.
 * Renders the Sidebar and Topbar, then positions the main content area
 * offset from the fixed sidebar (250px) and topbar (60px).
 */
import Sidebar from './Sidebar.jsx';
import Topbar from './Topbar.jsx';
import './DashboardLayout.css';

export default function DashboardLayout({ children }) {
  return (
    <div className="dashboard-layout">
      <Sidebar />
      <Topbar />
      <main className="dashboard-main">
        <div className="dashboard-content">{children}</div>
      </main>
    </div>
  );
}
