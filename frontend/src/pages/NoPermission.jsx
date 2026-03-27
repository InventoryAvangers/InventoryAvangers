/**
 * NoPermission
 *
 * Shown when an authenticated user tries to access a feature that is not
 * available on their current plan or role. Supports light/dark themes via
 * the global auth store. Includes a button to navigate back to the dashboard.
 */
import { useNavigate } from 'react-router-dom';
import { FiLock } from 'react-icons/fi';
import useAuthStore from '../store/authStore.js';
import './NoPermission.css';

export default function NoPermission({ feature }) {
  const navigate = useNavigate();
  const theme = useAuthStore((s) => s.theme);

  return (
    <div className={`no-perm-page ${theme === 'dark' ? 'no-perm-page--dark' : 'no-perm-page--light'}`}>
      <div className="no-perm-content">
        <div className={`no-perm-icon ${theme === 'dark' ? 'no-perm-icon--dark' : 'no-perm-icon--light'}`}>
          <FiLock size={36} />
        </div>
        <h1 className={`no-perm-title ${theme === 'dark' ? 'no-perm-title--dark' : 'no-perm-title--light'}`}>
          You don&apos;t have access to this feature
        </h1>
        <p className={`no-perm-text ${theme === 'dark' ? 'no-perm-text--dark' : 'no-perm-text--light'}`}>
          This feature is not available on your current plan. Contact your store administrator to request access.
        </p>
        <button
          onClick={() => navigate('/dashboard')}
          className="btn btn-primary no-perm-back-btn"
        >
          Go Back
        </button>
      </div>
    </div>
  );
}
