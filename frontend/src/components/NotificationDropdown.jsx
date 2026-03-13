/**
 * NotificationDropdown — bell icon button with unread count badge.
 * Opens a dropdown panel listing recent notifications; supports mark-as-read.
 */
import { useState, useEffect, useRef, useCallback } from 'react';
import { FiBell } from 'react-icons/fi';
import { apiGet, apiPut } from '../api/axios.js';
import { fmtDate } from '../utils/helpers.js';
import './NotificationDropdown.css';

export default function NotificationDropdown() {
  const [open, setOpen] = useState(false);
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const ref = useRef(null);

  const loadNotifications = useCallback(async () => {
    try {
      const data = await apiGet('/notifications');
      setNotifications(data.data || []);
      setUnreadCount(data.unreadCount || 0);
    } catch {
      // non-blocking
    }
  }, []);

  useEffect(() => {
    loadNotifications();
    const interval = setInterval(loadNotifications, 30000);
    return () => clearInterval(interval);
  }, [loadNotifications]);

  useEffect(() => {
    const handler = (e) => {
      if (ref.current && !ref.current.contains(e.target)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const markRead = async (id) => {
    try {
      await apiPut(`/notifications/${id}/read`);
      setNotifications((prev) =>
        prev.map((n) => (n._id === id ? { ...n, read: true } : n))
      );
      setUnreadCount((c) => Math.max(0, c - 1));
    } catch {
      // non-blocking
    }
  };

  const markAllRead = async () => {
    try {
      await apiPut('/notifications/read-all');
      setNotifications((prev) => prev.map((n) => ({ ...n, read: true })));
      setUnreadCount(0);
    } catch {
      // non-blocking
    }
  };

  return (
    <div className="notif-wrap" ref={ref}>
      <button
        onClick={() => setOpen((v) => !v)}
        className="notif-btn"
        aria-label="Notifications"
      >
        <FiBell size={18} />
        {unreadCount > 0 && (
          <span className="notif-badge">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="notif-dropdown">
          <div className="notif-dropdown-header">
            <span className="notif-dropdown-title">Notifications</span>
            {unreadCount > 0 && (
              <button onClick={markAllRead} className="notif-mark-all-btn">
                Mark all read
              </button>
            )}
          </div>

          <div className="notif-list">
            {notifications.length === 0 ? (
              <p className="notif-empty">No notifications</p>
            ) : (
              notifications.map((n) => (
                <div
                  key={n._id}
                  onClick={() => !n.read && markRead(n._id)}
                  className={`notif-item${!n.read ? ' notif-item--unread' : ''}`}
                >
                  <div className="notif-item-inner">
                    {!n.read && <span className="notif-unread-dot" />}
                    <div className={!n.read ? 'notif-item-body' : 'notif-item-body notif-item-body--offset'}>
                      <p className="notif-item-title">{n.title}</p>
                      <p className="notif-item-message">{n.message}</p>
                      <p className="notif-item-date">{fmtDate(n.createdAt)}</p>
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}
