/**
 * Modal — accessible dialog overlay with focus trap and focus restore.
 * Uses global .modal-backdrop, .modal-container, .modal-sm/md/lg/xl,
 * .modal-header, .modal-title, .modal-close-btn, .modal-body classes.
 */
import { useEffect, useRef, useId } from 'react';
import { FiX } from 'react-icons/fi';

export default function Modal({ isOpen, onClose, title, children, size = 'md' }) {
  const containerRef = useRef(null);
  const titleId = useId();

  // Escape key + focus management
<<<<<<< HEAD
=======
  const onCloseRef = useRef(onClose);
  useEffect(() => { onCloseRef.current = onClose; }, [onClose]);

>>>>>>> 5a73710 (feat(ui): add loading spinner, modal and alert reusable components)
  useEffect(() => {
    if (!isOpen) return;
    const prevFocus = document.activeElement;
    // Defer focus so the container is painted before we move focus into it
    const raf = requestAnimationFrame(() => containerRef.current?.focus());
<<<<<<< HEAD
    const handler = (e) => { if (e.key === 'Escape') onClose?.(); };
=======
    const handler = (e) => { if (e.key === 'Escape') onCloseRef.current?.(); };
>>>>>>> 5a73710 (feat(ui): add loading spinner, modal and alert reusable components)
    document.addEventListener('keydown', handler);
    return () => {
      cancelAnimationFrame(raf);
      document.removeEventListener('keydown', handler);
      // Restore focus to the element that opened the modal
      if (prevFocus && typeof prevFocus.focus === 'function') prevFocus.focus();
    };
<<<<<<< HEAD
  }, [isOpen, onClose]);
=======
  }, [isOpen]);
>>>>>>> 5a73710 (feat(ui): add loading spinner, modal and alert reusable components)

  // Focus trap – keep Tab/Shift+Tab cycling inside the dialog
  useEffect(() => {
    if (!isOpen || !containerRef.current) return;
    const container = containerRef.current;
    const FOCUSABLE =
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    const handler = (e) => {
      if (e.key !== 'Tab') return;
      const nodes = [...container.querySelectorAll(FOCUSABLE)];
      if (!nodes.length) return;
      const first = nodes[0];
      const last = nodes[nodes.length - 1];
      if (e.shiftKey) {
        if (document.activeElement === first || document.activeElement === container) {
          e.preventDefault();
          last.focus();
        }
      } else {
        if (document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };
    container.addEventListener('keydown', handler);
    return () => container.removeEventListener('keydown', handler);
  }, [isOpen]);

  if (!isOpen) return null;

  const sizeClass = {
    sm: 'modal-sm',
    md: 'modal-md',
    lg: 'modal-lg',
    xl: 'modal-xl',
  }[size] || 'modal-md';

  return (
    <div
      className="modal-backdrop"
      onClick={(e) => { if (e.target === e.currentTarget) onClose?.(); }}
    >
      <div
        ref={containerRef}
        className={`modal-container ${sizeClass}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={title ? titleId : undefined}
        tabIndex={-1}
      >
        {title && (
          <div className="modal-header">
            <h2 id={titleId} className="modal-title">{title}</h2>
            <button onClick={onClose} className="modal-close-btn" aria-label="Close dialog">
              <FiX size={20} />
            </button>
          </div>
        )}
        <div className="modal-body">{children}</div>
      </div>
    </div>
  );
}
