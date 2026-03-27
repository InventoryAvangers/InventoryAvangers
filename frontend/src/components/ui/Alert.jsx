/**
 * Alert — dismissible status message banner.
 * Uses global .alert, .alert-success, .alert-error, .alert-warning, .alert-info classes.
 */
import { useEffect } from 'react';
import { FiCheckCircle, FiAlertCircle, FiInfo, FiAlertTriangle, FiX } from 'react-icons/fi';

const ICONS = {
  success: FiCheckCircle,
  error: FiAlertCircle,
  warning: FiAlertTriangle,
  info: FiInfo,
};

export default function Alert({ message, type = 'error', onClose, autoClose = 5000 }) {
  const Icon = ICONS[type] || FiInfo;

  useEffect(() => {
    if (!message || !autoClose) return;
    const t = setTimeout(() => onClose?.(), autoClose);
    return () => clearTimeout(t);
  }, [message, autoClose, onClose]);

  if (!message) return null;

  return (
    <div className={`alert alert-${type === 'error' ? 'error' : type}`}>
      <Icon size={16} style={{ flexShrink: 0, marginTop: '1px' }} />
      <span style={{ flex: 1 }}>{message}</span>
      {onClose && (
        <button
          type="button"
          onClick={onClose}
          className="alert-close"
          aria-label="Dismiss alert"
        >
          <FiX size={14} />
        </button>
      )}
    </div>
  );
}
