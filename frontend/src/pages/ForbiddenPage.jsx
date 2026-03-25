/**
 * ForbiddenPage
 *
 * Displayed when an authenticated user attempts to access a route they are
 * not authorised to view (HTTP 403). Shows a lock icon, a 403 error code,
 * a brief explanation, and a link back to the dashboard.
 */
import { Link } from 'react-router-dom';
import { FiLock } from 'react-icons/fi';
import './ForbiddenPage.css';

export default function ForbiddenPage() {
  return (
    <div className="forbidden-page">
      <div className="forbidden-content">
        <div className="forbidden-icon">
          <FiLock size={36} />
        </div>
        <h1 className="forbidden-code">403</h1>
        <h2 className="forbidden-title">Access Forbidden</h2>
        <p className="forbidden-text">
          You don&apos;t have permission to access this page. Please contact your administrator if you believe this is an error.
        </p>
        <Link to="/dashboard" className="btn btn-primary forbidden-back-btn">
          Back to Dashboard
        </Link>
      </div>
    </div>
  );
}
