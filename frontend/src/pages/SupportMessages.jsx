/**
 * SupportMessages page — allows users to contact the platform support team.
 * Provides an inbox for received messages (with unread indicators), a sent
 * folder, and a compose form that supports threading replies to existing
 * messages. New messages are sent directly to the platform admin team.
 */
import { useState, useEffect, useCallback } from 'react';
import { FiSend, FiInbox, FiMessageSquare, FiRefreshCw } from 'react-icons/fi';
import DashboardLayout from '../components/layout/DashboardLayout.jsx';
import Alert from '../components/ui/Alert.jsx';
import LoadingSpinner from '../components/ui/LoadingSpinner.jsx';
import { apiGet, apiPost, apiPatch, apiErrMsg } from '../api/axios.js';
import { fmtDate } from '../utils/helpers.js';
import './SupportMessages.css';

export default function SupportMessages() {
  const [tab, setTab] = useState('inbox');
  const [inbox, setInbox] = useState([]);
  const [sent, setSent] = useState([]);
  const [loading, setLoading] = useState(true);
  const [alert, setAlert] = useState(null);
  const [compose, setCompose] = useState({ subject: '', body: '' });
  const [sending, setSending] = useState(false);
  const [replyTo, setReplyTo] = useState(null);

  const showAlert = (msg, type = 'error') => setAlert({ message: msg, type });
  const clearAlert = () => setAlert(null);

  const loadMessages = useCallback(async () => {
    setLoading(true);
    try {
      const [inboxData, sentData] = await Promise.all([
        apiGet('/messages/inbox'),
        apiGet('/messages/sent'),
      ]);
      setInbox(inboxData.data || []);
      setSent(sentData.data || []);
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadMessages(); }, [loadMessages]);

  const handleSend = async (e) => {
    e.preventDefault();
    if (!compose.subject.trim() || !compose.body.trim()) {
      showAlert('Subject and message body are required.');
      return;
    }
    setSending(true);
    try {
      await apiPost('/messages', {
        subject: compose.subject,
        body: compose.body,
        parentMessageId: replyTo?._id || undefined,
      });
      showAlert('Message sent successfully.', 'success');
      setCompose({ subject: '', body: '' });
      setReplyTo(null);
      await loadMessages();
      setTab('sent');
    } catch (err) {
      showAlert(apiErrMsg(err));
    } finally {
      setSending(false);
    }
  };

  const markRead = async (id) => {
    try {
      await apiPatch(`/messages/${id}/read`);
      setInbox((prev) => prev.map((m) => m._id === id ? { ...m, read: true } : m));
    } catch { /* non-critical */ }
  };

  const handleReply = (msg) => {
    setReplyTo(msg);
    setCompose({ subject: `Re: ${msg.subject}`, body: '' });
    setTab('compose');
  };

  const unreadCount = inbox.filter((m) => !m.read).length;

  return (
    <DashboardLayout>
      <div className="support-page-header">
        <div>
          <h2 className="support-page-title">Help &amp; Support</h2>
          <p className="support-page-subtitle">Contact the platform team for assistance</p>
        </div>
        <button onClick={loadMessages} className="btn btn-outline btn-sm" title="Refresh">
          <FiRefreshCw size={13} />
        </button>
      </div>

      {alert && <Alert message={alert.message} type={alert.type} onClose={clearAlert} />}

      {/* Tabs */}
      <div className="support-tabs">
        {[
          { key: 'inbox', label: `Inbox${unreadCount > 0 ? ` (${unreadCount})` : ''}`, icon: <FiInbox size={14} /> },
          { key: 'sent', label: 'Sent', icon: <FiSend size={14} /> },
          { key: 'compose', label: 'New Message', icon: <FiMessageSquare size={14} /> },
        ].map(({ key, label, icon }) => (
          <button
            key={key}
            onClick={() => { setTab(key); if (key === 'compose' && !replyTo) setCompose({ subject: '', body: '' }); }}
            className={`support-tab-btn${tab === key ? ' support-tab-btn--active' : ''}`}
          >
            {icon} {label}
          </button>
        ))}
      </div>

      {loading && tab !== 'compose' ? (
        <LoadingSpinner />
      ) : (
        <>
          {/* Inbox */}
          {tab === 'inbox' && (
            inbox.length === 0 ? (
              <div className="card support-empty-card">
                <FiInbox size={40} className="support-empty-icon" />
                <p className="support-empty-text">No messages in your inbox.</p>
              </div>
            ) : (
              <div className="support-message-list">
                {inbox.map((msg) => (
                  <div
                    key={msg._id}
                    className={`card support-message-card${!msg.read ? ' support-message-card--unread' : ''}`}
                    onClick={() => markRead(msg._id)}
                  >
                    <div className="support-message-card-inner">
                      <div className="support-message-content">
                        <div className="support-message-title-row">
                          {!msg.read && <span className="support-unread-dot" />}
                          <p className={`support-message-subject${!msg.read ? ' support-message-subject--bold' : ' support-message-subject--medium'}`}>
                            {msg.subject}
                          </p>
                        </div>
                        <p className="support-message-meta">
                          From: {msg.fromId?.name || 'Support Team'} · {fmtDate(msg.sentAt)}
                        </p>
                        <p className="support-message-preview">{msg.body}</p>
                      </div>
                      <button
                        onClick={(e) => { e.stopPropagation(); handleReply(msg); }}
                        className="btn btn-outline btn-sm shrink-0"
                      >
                        Reply
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )
          )}

          {/* Sent */}
          {tab === 'sent' && (
            sent.length === 0 ? (
              <div className="card support-empty-card">
                <FiSend size={40} className="support-empty-icon" />
                <p className="support-empty-text">No sent messages yet.</p>
              </div>
            ) : (
              <div className="support-message-list">
                {sent.map((msg) => (
                  <div key={msg._id} className="card support-message-card">
                    <p className="support-message-subject support-message-subject--bold">{msg.subject}</p>
                    <p className="support-message-meta">
                      To: {msg.toId?.name || 'Support Team'} · {fmtDate(msg.sentAt)}
                    </p>
                    <p className="support-message-preview">{msg.body}</p>
                  </div>
                ))}
              </div>
            )
          )}

          {/* Compose */}
          {tab === 'compose' && (
            <div className="support-compose-card card">
              {replyTo && (
                <div className="support-reply-context">
                  <span className="support-reply-context-label">Replying to:</span> {replyTo.subject}
                  <button
                    onClick={() => { setReplyTo(null); setCompose({ subject: '', body: '' }); }}
                    className="support-reply-clear"
                  >
                    ✕
                  </button>
                </div>
              )}
              <form onSubmit={handleSend} className="support-compose-form">
                <div>
                  <label className="form-label">Subject *</label>
                  <input
                    className="form-control"
                    value={compose.subject}
                    onChange={(e) => setCompose({ ...compose, subject: e.target.value })}
                    placeholder="Brief description of your issue"
                    required
                  />
                </div>
                <div>
                  <label className="form-label">Message *</label>
                  <textarea
                    className="form-control"
                    rows={6}
                    value={compose.body}
                    onChange={(e) => setCompose({ ...compose, body: e.target.value })}
                    placeholder="Describe your issue in detail..."
                    required
                  />
                </div>
                <div>
                  <button type="submit" className="btn btn-primary" disabled={sending}>
                    <FiSend size={14} />
                    {sending ? 'Sending...' : 'Send Message'}
                  </button>
                </div>
              </form>
            </div>
          )}
        </>
      )}
    </DashboardLayout>
  );
}
