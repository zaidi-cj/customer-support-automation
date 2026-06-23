import React, { useState, useEffect } from 'react';
import { 
  Inbox, 
  RefreshCw, 
  CheckCircle2, 
  XCircle, 
  Sparkles, 
  Database, 
  Plus, 
  Clock, 
  Edit3, 
  User, 
  BookOpen, 
  AlertCircle,
  FileText,
  Activity,
  ArrowRight
} from 'lucide-react';


// API Configuration
const DEFAULT_API_URL = 'http://localhost:5144'; // Standard ASP.NET Core port or customizable

interface Ticket {
  id: string;
  customerEmail: string;
  subject: string;
  body: string;
  status: string;
  intent: string | null;
  category: string | null;
  metadataJson: string | null;
  createdAt: string;
  updatedAt: string;
  logs?: AgentLog[];
  drafts?: TicketDraft[];
}

interface AgentLog {
  id: string;
  ticketId: string;
  agentName: string;
  action: string;
  input: string;
  output: string;
  status: string;
  createdAt: string;
}

interface TicketDraft {
  id: string;
  ticketId: string;
  content: string;
  reviewScore: number;
  reviewFeedback: string | null;
  operatorComments: string | null;
  status: string;
  createdAt: string;
  actionedAt: string | null;
}

interface KnowledgeDocument {
  id: string;
  title: string;
  content: string;
  category: string;
  createdAt: string;
}

const EMAIL_PRESETS = [
  {
    label: "Order Status Tracking (Order 10293)",
    email: "jane.doe@gmail.com",
    subject: "Where is order 10293? I haven't received it yet!",
    body: "Hi Support Team, I ordered a package last week. Order number is 10293. The portal says it shipped, but I haven't received it and the tracking link isn't working. Can you please check where it is and tell me when it will arrive? Thank you, Jane."
  },
  {
    label: "Refund Policy Query",
    email: "john.doe@example.com",
    subject: "Requesting a refund for my order",
    body: "Hello, I purchased some products from your website 10 days ago (order 99482). Unfortunately, they do not fit my needs. Can I return them and get a full refund back to my card? Let me know what steps I need to take. Best, John."
  },
  {
    label: "Account Access Lockout",
    email: "alice.smith@test.com",
    subject: "Locked out of my dashboard!",
    body: "Hello, I tried logging in to my account standard portal but I got my password wrong a few times and now it says my account is locked out! Can you please unlock it for me immediately? I need to print an invoice. Thanks, Alice."
  },
  {
    label: "Angry Customer Escalation",
    email: "vip.buyer@gemini.com",
    subject: "TERRIBLE SERVICE - I WANT A REFUND",
    body: "I have been waiting for weeks for a response on my refund request and nobody is replying to my emails! This is completely unacceptable. I demand to speak to a manager or supervisor immediately or I will file a chargeback with my bank!"
  }
];

export default function App() {
  const [apiUrl, setApiUrl] = useState(() => {
    return localStorage.getItem('agentflow_api_url') || DEFAULT_API_URL;
  });
  const [activeTab, setActiveTab] = useState<'inbox' | 'knowledge'>('inbox');
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [selectedTicketId, setSelectedTicketId] = useState<string | null>(null);
  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const [knowledgeDocs, setKnowledgeDocs] = useState<KnowledgeDocument[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isApiConnected, setIsApiConnected] = useState(false);
  
  // Modals and Forms State
  const [showNewTicketModal, setShowNewTicketModal] = useState(false);
  const [newTicketEmail, setNewTicketEmail] = useState('');
  const [newTicketSubject, setNewTicketSubject] = useState('');
  const [newTicketBody, setNewTicketBody] = useState('');
  
  const [showNewDocModal, setShowNewDocModal] = useState(false);
  const [newDocTitle, setNewDocTitle] = useState('');
  const [newDocCategory, setNewDocCategory] = useState('Refund');
  const [newDocContent, setNewDocContent] = useState('');

  // Operator Action State
  const [editedDraftContent, setEditedDraftContent] = useState('');
  const [loadedDraftId, setLoadedDraftId] = useState<string | null>(null);
  const [rejectionComments, setRejectionComments] = useState('');
  const [showRejectionInput, setShowRejectionInput] = useState(false);
  const [isSubmittingAction, setIsSubmittingAction] = useState(false);

  // Poll for tickets list updates and selected ticket updates when processing
  useEffect(() => {
    localStorage.setItem('agentflow_api_url', apiUrl);
    checkApiConnection();
    fetchTickets(true);
    fetchKnowledgeDocs();
  }, [apiUrl]);

  // Poll active ticket details every 3 seconds to update timeline and sidebar queue
  useEffect(() => {
    let interval: number;
    if (selectedTicketId) {
      fetchTicketDetails(selectedTicketId);
      
      interval = window.setInterval(() => {
        fetchTicketDetails(selectedTicketId);
        fetchTickets(); // Refresh sidebar queue too
      }, 3000);
    }
    return () => clearInterval(interval);
  }, [selectedTicketId]);

  // Synchronize draft editor text area with active ticket drafts reactively
  useEffect(() => {
    if (selectedTicket) {
      const drafts = selectedTicket.drafts || [];
      const latest = [...drafts].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
      if (latest) {
        if (loadedDraftId !== latest.id) {
          setEditedDraftContent(latest.content);
          setLoadedDraftId(latest.id);
        }
      } else {
        setEditedDraftContent('');
        setLoadedDraftId(null);
      }
    } else {
      setEditedDraftContent('');
      setLoadedDraftId(null);
    }
  }, [selectedTicket, loadedDraftId]);

  const checkApiConnection = async () => {
    try {
      const res = await fetch(`${apiUrl}/api/tickets`);
      if (res.ok) {
        setIsApiConnected(true);
      } else {
        setIsApiConnected(false);
      }
    } catch {
      setIsApiConnected(false);
    }
  };

  const fetchTickets = async (autoSelect: any = false) => {
    try {
      const res = await fetch(`${apiUrl}/api/tickets`);
      if (res.ok) {
        const data = await res.json();
        setTickets(data);
        setIsApiConnected(true);
        if (autoSelect === true && data.length > 0 && !selectedTicketId) {
          handleSelectTicket(data[0].id);
        }
      }
    } catch (err) {
      console.error("Error fetching tickets:", err);
      setIsApiConnected(false);
    }
  };

  const fetchTicketDetails = async (id: string) => {
    try {
      const res = await fetch(`${apiUrl}/api/tickets/${id}`);
      if (res.ok) {
        const data = await res.json();
        setSelectedTicket(data);
      }
    } catch (err) {
      console.error("Error fetching ticket details:", err);
    }
  };

  const fetchKnowledgeDocs = async () => {
    try {
      const res = await fetch(`${apiUrl}/api/knowledge`);
      if (res.ok) {
        const data = await res.json();
        setKnowledgeDocs(data);
      }
    } catch (err) {
      console.error("Error fetching knowledge documents:", err);
    }
  };

  const handleSelectTicket = (id: string) => {
    setSelectedTicketId(id);
    setSelectedTicket(null);
    setRejectionComments('');
    setShowRejectionInput(false);
    fetchTicketDetails(id);
  };

  const handleReconnect = async () => {
    setIsLoading(true);
    try {
      await checkApiConnection();
      await fetchTickets();
      await fetchKnowledgeDocs();
      if (selectedTicketId) {
        await fetchTicketDetails(selectedTicketId);
      }
    } catch (err) {
      console.error("Error reconnecting API:", err);
    } finally {
      setIsLoading(false);
    }
  };

  const getIntentClass = (intent: string | null) => {
    if (!intent) return '';
    const clean = intent.trim().toLowerCase();
    if (clean.includes('refund')) return 'intent-refund';
    if (clean.includes('shipping') || clean.includes('order')) return 'intent-shipping';
    if (clean.includes('tech') || clean.includes('lockout') || clean.includes('support')) return 'intent-techsupport';
    return 'intent-default';
  };

  const isProcessingStatus = (status: string) => {
    return ['Classifying', 'Retrieving', 'Drafting', 'Reviewing', 'Sending'].includes(status);
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Received': return 'status-received';
      case 'Classifying':
      case 'Retrieving':
      case 'Drafting':
      case 'Reviewing': return 'status-processing';
      case 'PendingApproval': return 'status-pending-approval';
      case 'Approved':
      case 'Sent': return 'status-sent';
      case 'Rejected': return 'status-rejected';
      case 'Failed': return 'status-failed';
      default: return 'status-default';
    }
  };

  const selectPreset = (preset: typeof EMAIL_PRESETS[0]) => {
    setNewTicketEmail(preset.email);
    setNewTicketSubject(preset.subject);
    setNewTicketBody(preset.body);
  };

  const handleCreateTicketSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTicketEmail || !newTicketSubject || !newTicketBody) return;
    setIsLoading(true);

    try {
      const res = await fetch(`${apiUrl}/api/tickets`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          customerEmail: newTicketEmail,
          subject: newTicketSubject,
          body: newTicketBody
        })
      });

      if (res.ok) {
        const createdTicket = await res.json();
        setShowNewTicketModal(false);
        setNewTicketEmail('');
        setNewTicketSubject('');
        setNewTicketBody('');
        fetchTickets();
        handleSelectTicket(createdTicket.id);
      } else {
        alert("Failed to submit ticket.");
      }
    } catch (err) {
      alert("Error contacting API database server. Ensure backend is running.");
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCreateDocSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newDocTitle || !newDocContent || !newDocCategory) return;
    setIsLoading(true);

    try {
      const res = await fetch(`${apiUrl}/api/knowledge`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          title: newDocTitle,
          category: newDocCategory,
          content: newDocContent
        })
      });

      if (res.ok) {
        setShowNewDocModal(false);
        setNewDocTitle('');
        setNewDocContent('');
        fetchKnowledgeDocs();
      } else {
        alert("Failed to index knowledge base article.");
      }
    } catch (err) {
      alert("Error contacting API database server.");
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleApprove = async () => {
    if (!selectedTicket) return;
    setIsSubmittingAction(true);
    try {
      const res = await fetch(`${apiUrl}/api/approval/${selectedTicket.id}/approve`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          editedContent: editedDraftContent
        })
      });
      if (res.ok) {
        fetchTickets();
        fetchTicketDetails(selectedTicket.id);
        alert("Draft approved! Response dispatched to customer.");
      } else {
        alert("Failed to approve draft.");
      }
    } catch (err) {
      console.error(err);
    } finally {
      setIsSubmittingAction(false);
    }
  };

  const handleReject = async () => {
    if (!selectedTicket || !rejectionComments) return;
    setIsSubmittingAction(true);
    try {
      const res = await fetch(`${apiUrl}/api/approval/${selectedTicket.id}/reject`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          comments: rejectionComments
        })
      });
      if (res.ok) {
        setShowRejectionInput(false);
        setRejectionComments('');
        setEditedDraftContent('');
        fetchTickets();
        fetchTicketDetails(selectedTicket.id);
      } else {
        alert("Failed to reject draft.");
      }
    } catch (err) {
      console.error(err);
    } finally {
      setIsSubmittingAction(false);
    }
  };

  const triggerRerun = async () => {
    if (!selectedTicket) return;
    try {
      await fetch(`${apiUrl}/api/tickets/${selectedTicket.id}/run`, { method: 'POST' });
      fetchTickets();
      fetchTicketDetails(selectedTicket.id);
    } catch (err) {
      console.error(err);
    }
  };

  // Helper selectors
  const latestDraft = selectedTicket?.drafts 
    ? [...selectedTicket.drafts].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0]
    : null;

  const parsedMetadata = selectedTicket?.metadataJson 
    ? JSON.parse(selectedTicket.metadataJson) 
    : null;

  return (
    <div className="app-container">
      {/* Sidebar Navigation */}
      <aside className="sidebar">
        <div className="brand">
          <Sparkles className="logo-icon" />
          <span className="brand-name">AgentFlow</span>
          <span className="brand-badge">SK v1.15</span>
        </div>

        <button className="btn btn-primary new-ticket-btn" onClick={() => setShowNewTicketModal(true)}>
          <Plus size={16} />
          <span>New Customer Ticket</span>
        </button>

        <nav className="nav-tabs">
          <button 
            className={`nav-tab ${activeTab === 'inbox' ? 'active' : ''}`}
            onClick={() => setActiveTab('inbox')}
          >
            <Inbox size={18} />
            <span>Support Inbox</span>
            {tickets.filter(t => t.status === 'PendingApproval').length > 0 && (
              <span className="badge-count">{tickets.filter(t => t.status === 'PendingApproval').length}</span>
            )}
          </button>
          <button 
            className={`nav-tab ${activeTab === 'knowledge' ? 'active' : ''}`}
            onClick={() => setActiveTab('knowledge')}
          >
            <Database size={18} />
            <span>Knowledge Base</span>
          </button>
        </nav>

        {/* API connection indicator */}
        <div className="api-config-panel">
          <div className="api-status">
            <span className={`status-dot ${isApiConnected ? 'connected' : 'disconnected'}`}></span>
            <span className="status-text">{isApiConnected ? 'API Connected' : 'API Offline'}</span>
          </div>
          <div className="api-url-input-container">
            <input 
              type="text" 
              value={apiUrl}
              onChange={(e) => setApiUrl(e.target.value)}
              placeholder="API endpoint"
              className="api-url-input"
            />
            <button className="btn-icon" onClick={handleReconnect} title="Refresh connection" disabled={isLoading}>
              <RefreshCw size={12} className={isLoading ? 'spin-icon' : ''} />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content Pane */}
      <main className="main-content">
        {activeTab === 'inbox' ? (
          <div className="inbox-layout">
            {/* Tickets list */}
            <div className="ticket-list-panel">
              <div className="panel-header">
                <h2>Customer Tickets</h2>
                <button className="btn-icon" onClick={fetchTickets} title="Reload list">
                  <RefreshCw size={16} />
                </button>
              </div>
              <div className="tickets-scroll-list">
                {tickets.length === 0 ? (
                  <div className="empty-state">
                    <Inbox size={32} />
                    <p>No tickets found. Create a simulation ticket to begin.</p>
                  </div>
                ) : (
                  tickets.map(ticket => (
                    <div 
                      key={ticket.id}
                      className={`ticket-card ${selectedTicketId === ticket.id ? 'active' : ''}`}
                      onClick={() => handleSelectTicket(ticket.id)}
                    >
                      <div className="ticket-card-header">
                        <span className="ticket-email">{ticket.customerEmail}</span>
                        <span className="ticket-time">{new Date(ticket.createdAt).toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}</span>
                      </div>
                      <h4 className="ticket-subject">{ticket.subject}</h4>
                      <div className="ticket-card-footer">
                        <span className={`badge ${getStatusColor(ticket.status)}`}>
                          {ticket.status}
                        </span>
                        {ticket.intent && (
                          <span className={`ticket-intent-badge ${getIntentClass(ticket.intent)}`}>{ticket.intent}</span>
                        )}
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* Ticket details */}
            <div className="ticket-detail-panel">
              {selectedTicketId ? (
                selectedTicket ? (
                  <div className="detail-layout">
                    {/* Ticket Header */}
                    <div className="detail-header">
                      <div>
                        <div className="header-meta">
                          <span className="customer-tag">
                            <User size={14} style={{ marginRight: '4px' }} />
                            {selectedTicket.customerEmail}
                          </span>
                          <span className={`badge ${getStatusColor(selectedTicket.status)}`}>
                            {selectedTicket.status}
                          </span>
                          {selectedTicket.intent && (
                            <span className="badge intent-tag">
                              Intent: {selectedTicket.intent} ({selectedTicket.category || 'General'})
                            </span>
                          )}
                        </div>
                        <h2 className="detail-title">{selectedTicket.subject}</h2>
                      </div>
                      <div className="detail-actions">
                        {isProcessingStatus(selectedTicket.status) && (
                          <div className="spinner-container">
                            <RefreshCw size={16} className="spin-icon" />
                            <span>Agents working...</span>
                          </div>
                        )}
                        <button className="btn btn-secondary" onClick={triggerRerun}>
                          <RefreshCw size={14} />
                          <span>Rerun Workflow</span>
                        </button>
                      </div>
                    </div>

                    {/* Metadata & CRM lookup card */}
                    {parsedMetadata && (parsedMetadata.customerName || parsedMetadata.orderId) && (
                      <div className="crm-info-banner">
                        <div className="crm-banner-title">
                          <Activity size={14} />
                          <span>Agent Extracted Context</span>
                        </div>
                        <div className="crm-banner-grid">
                          {parsedMetadata.customerName && (
                            <div className="crm-banner-item">
                              <span className="crm-label">Customer Name:</span>
                              <span className="crm-value">{parsedMetadata.customerName}</span>
                            </div>
                          )}
                          {parsedMetadata.accountId && (
                            <div className="crm-banner-item">
                              <span className="crm-label">Account ID:</span>
                              <span className="crm-value">{parsedMetadata.accountId}</span>
                            </div>
                          )}
                          {parsedMetadata.orderId && (
                            <div className="crm-banner-item">
                              <span className="crm-label">Order Number:</span>
                              <span className="crm-value">{parsedMetadata.orderId}</span>
                            </div>
                          )}
                        </div>
                      </div>
                    )}

                    {/* Two column split */}
                    <div className="detail-grid">
                      {/* Left: Email Body + Agent Logs */}
                      <div className="detail-left-col">
                        <div className="section-card email-body-card">
                          <div className="card-header">
                            <Inbox size={16} />
                            <h3>Original Customer Email</h3>
                          </div>
                          <div className="email-body-content">
                            {selectedTicket.body}
                          </div>
                        </div>

                        <div className="section-card logs-card">
                          <div className="card-header">
                            <Activity size={16} />
                            <h3>Agent Execution Logs</h3>
                          </div>
                          <div className="logs-timeline">
                            {selectedTicket.logs && selectedTicket.logs.length > 0 ? (
                              selectedTicket.logs
                                .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
                                .map((log, index) => (
                                  <div key={log.id} className="timeline-item">
                                    <div className="timeline-connector"></div>
                                    <div className="timeline-node">
                                      <span className="timeline-index">{index + 1}</span>
                                    </div>
                                    <div className="timeline-content">
                                      <div className="timeline-header">
                                        <h4 className="timeline-agent">{log.agentName}</h4>
                                        <span className="timeline-action">{log.action}</span>
                                        <span className={`timeline-status ${log.status.toLowerCase()}`}>
                                          {log.status}
                                        </span>
                                      </div>
                                      <div className="timeline-body">
                                        <div className="timeline-block">
                                          <div className="timeline-block-label">Input Parameters</div>
                                          <pre className="timeline-block-pre">{log.input}</pre>
                                        </div>
                                        <div className="timeline-block">
                                          <div className="timeline-block-label">Thought & Outputs</div>
                                          <pre className="timeline-block-pre highlight">{log.output}</pre>
                                        </div>
                                      </div>
                                    </div>
                                  </div>
                                ))
                            ) : (
                              <div className="empty-logs">
                                <Clock size={20} />
                                <p>No logs found. Waiting for workflow execution to start...</p>
                              </div>
                            )}
                          </div>
                        </div>
                      </div>

                      {/* Right: RAG Docs & Human Gate */}
                      <div className="detail-right-col">
                        {/* Vector Database Retrieval Results (RAG) */}
                        <div className="section-card rag-card">
                          <div className="card-header">
                            <BookOpen size={16} />
                            <h3>RAG Sources (Vector DB)</h3>
                          </div>
                          <div className="rag-docs-list">
                            {/* Find retrieved documents in logs output or details */}
                            {(() => {
                              const latestRagLog = selectedTicket.logs
                                ? [...selectedTicket.logs]
                                    .filter(l => l.agentName === 'Knowledge Retrieval Agent' && l.status === 'Success' && l.output.includes("Retrieved"))
                                    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0]
                                : null;
                              return latestRagLog ? (
                                <div className="rag-doc-wrapper">
                                  <div className="rag-doc-desc">Based on keyword matching and cosine embedding query:</div>
                                  <pre className="rag-doc-pre">{latestRagLog.output}</pre>
                                </div>
                              ) : (
                                <p className="empty-rag-docs">No document context retrieved yet.</p>
                              );
                            })()}
                          </div>
                        </div>

                        {/* Human Gate approval dashboard */}
                        <div className="section-card approval-card">
                          <div className="card-header">
                            <Edit3 size={16} />
                            <h3>Human-in-the-Loop Review</h3>
                          </div>

                          {latestDraft ? (
                            <div className="approval-workspace">
                              {/* Score Banner */}
                              <div className="review-score-banner">
                                <div className="score-badge-circle" style={{
                                  borderColor: latestDraft.reviewScore >= 7.0 ? '#10b981' : '#f59e0b'
                                }}>
                                  <span className="score-val">{latestDraft.reviewScore.toFixed(1)}</span>
                                  <span className="score-max">/10</span>
                                </div>
                                <div className="score-details">
                                  <h4 className="score-title">Review Agent Score</h4>
                                  <p className="score-comment">{latestDraft.reviewFeedback || 'No review comments available.'}</p>
                                </div>
                              </div>

                              {/* Editor */}
                              <div className="draft-editor-container">
                                <div className="editor-label-container">
                                  <label className="editor-label">Draft Email Response</label>
                                  <span className="draft-version-badge">Version {selectedTicket.drafts?.length || 1}</span>
                                </div>
                                <textarea
                                  value={editedDraftContent}
                                  onChange={(e) => setEditedDraftContent(e.target.value)}
                                  className="draft-textarea"
                                  disabled={selectedTicket.status !== 'PendingApproval' || isSubmittingAction}
                                  rows={8}
                                />
                              </div>

                              {/* Reject/Approve Panel */}
                              {selectedTicket.status === 'PendingApproval' && (
                                <div className="approval-actions-container">
                                  {!showRejectionInput ? (
                                    <div className="action-buttons-row">
                                      <button 
                                        className="btn btn-danger flex-1"
                                        onClick={() => {
                                          setShowRejectionInput(true);
                                          setTimeout(() => {
                                            const rightCol = document.querySelector('.detail-right-col');
                                            if (rightCol) {
                                              rightCol.scrollTop = rightCol.scrollHeight;
                                            }
                                          }, 100);
                                        }}
                                      >
                                        <XCircle size={16} />
                                        <span>Reject & Rewrite</span>
                                      </button>
                                      <button 
                                        className="btn btn-success flex-1"
                                        onClick={handleApprove}
                                        disabled={isSubmittingAction}
                                      >
                                        <CheckCircle2 size={16} />
                                        <span>Approve & Send</span>
                                      </button>
                                    </div>
                                  ) : (
                                    <div className="rejection-form">
                                      <label className="rejection-label">Feedback for Writer Agent</label>
                                      <textarea
                                        value={rejectionComments}
                                        onChange={(e) => setRejectionComments(e.target.value)}
                                        placeholder="Explain what needs to be fixed or added. E.g., 'Do not mention refund shipping fee as it is already paid for. Please be softer.'"
                                        className="rejection-textarea"
                                        rows={3}
                                      />
                                      <div className="action-buttons-row">
                                        <button 
                                          className="btn btn-secondary"
                                          onClick={() => setShowRejectionInput(false)}
                                        >
                                          Cancel
                                        </button>
                                        <button 
                                          className="btn btn-danger flex-1"
                                          onClick={handleReject}
                                          disabled={!rejectionComments.trim() || isSubmittingAction}
                                        >
                                          Send Revision Request
                                          <ArrowRight size={14} style={{ marginLeft: '4px' }} />
                                        </button>
                                      </div>
                                    </div>
                                  )}
                                </div>
                              )}

                              {selectedTicket.status === 'Sent' && (
                                <div className="outcome-banner success">
                                  <CheckCircle2 size={18} />
                                  <span>Email successfully sent to customer.</span>
                                </div>
                              )}
                              
                              {selectedTicket.status === 'Sending' && (
                                <div className="outcome-banner sending">
                                  <RefreshCw size={18} className="spin-icon" />
                                  <span>Sending email...</span>
                                </div>
                              )}
                            </div>
                          ) : (
                            <div className="empty-approval">
                              <AlertCircle size={24} />
                              <p>No response draft has been generated yet.</p>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="loading-ticket-details">
                    <RefreshCw size={24} className="spin-icon" />
                    <p>Loading ticket execution traces...</p>
                  </div>
                )
              ) : (
                <div className="no-ticket-selected">
                  <Inbox size={48} />
                  <h3>Select a support ticket to start review</h3>
                  <p>Inspect step-by-step agent traces, vector database sources, and review generated response drafts.</p>
                </div>
              )}
            </div>
          </div>
        ) : (
          /* Knowledge Base Management Tab */
          <div className="knowledge-layout">
            <div className="knowledge-header">
              <div>
                <h2>Knowledge Base Index</h2>
                <p>Vector database sources configured for customer support RAG searches.</p>
              </div>
              <button className="btn btn-primary" onClick={() => setShowNewDocModal(true)}>
                <Plus size={16} />
                <span>Add Article</span>
              </button>
            </div>

            <div className="docs-grid">
              {knowledgeDocs.length === 0 ? (
                <div className="empty-docs-full">
                  <BookOpen size={48} />
                  <h3>No knowledge articles found</h3>
                  <p>Index articles (refund policies, support instructions) to enable agent RAG retrieval.</p>
                </div>
              ) : (
                knowledgeDocs.map(doc => (
                  <div key={doc.id} className="doc-card">
                    <div className="doc-card-meta">
                      <span className="doc-category-badge">{doc.category}</span>
                      <span className="doc-id">ID: {doc.id.substring(0,8)}</span>
                    </div>
                    <h3 className="doc-title">{doc.title}</h3>
                    <p className="doc-content-preview">{doc.content}</p>
                    <div className="doc-card-footer">
                      <FileText size={12} style={{ marginRight: '4px' }} />
                      <span>Vector Dimension: 1536 (OpenAI)</span>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        )}
      </main>

      {/* New Ticket Modal */}
      {showNewTicketModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Create Simulation Customer Email</h3>
              <button className="btn-close" onClick={() => setShowNewTicketModal(false)}>×</button>
            </div>
            
            {/* Presets List */}
            <div className="presets-section">
              <label className="presets-label">Quick Testing Presets:</label>
              <div className="presets-grid">
                {EMAIL_PRESETS.map((preset, index) => (
                  <button 
                    key={index} 
                    className="preset-btn"
                    onClick={() => selectPreset(preset)}
                  >
                    {preset.label}
                  </button>
                ))}
              </div>
            </div>

            <form onSubmit={handleCreateTicketSubmit} className="modal-form">
              <div className="form-group">
                <label>Customer Email</label>
                <input 
                  type="email" 
                  value={newTicketEmail}
                  onChange={(e) => setNewTicketEmail(e.target.value)}
                  placeholder="customer@example.com"
                  required
                />
              </div>
              <div className="form-group">
                <label>Email Subject</label>
                <input 
                  type="text" 
                  value={newTicketSubject}
                  onChange={(e) => setNewTicketSubject(e.target.value)}
                  placeholder="E.g., Issue with delivery"
                  required
                />
              </div>
              <div className="form-group">
                <label>Email Body</label>
                <textarea 
                  value={newTicketBody}
                  onChange={(e) => setNewTicketBody(e.target.value)}
                  placeholder="Type the customer email contents..."
                  rows={6}
                  required
                />
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setShowNewTicketModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary" disabled={isLoading}>
                  {isLoading ? 'Triggering Agents...' : 'Submit & Run Workflow'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* New Doc Modal */}
      {showNewDocModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Index Support Article</h3>
              <button className="btn-close" onClick={() => setShowNewDocModal(false)}>×</button>
            </div>
            <form onSubmit={handleCreateDocSubmit} className="modal-form">
              <div className="form-group">
                <label>Article Title</label>
                <input 
                  type="text" 
                  value={newDocTitle}
                  onChange={(e) => setNewDocTitle(e.target.value)}
                  placeholder="E.g., Shipping Fees and Free Delivery Tier"
                  required
                />
              </div>
              <div className="form-group">
                <label>Category</label>
                <select 
                  value={newDocCategory}
                  onChange={(e) => setNewDocCategory(e.target.value)}
                  className="modal-select"
                >
                  <option value="Refund">Refund</option>
                  <option value="Shipping">Shipping</option>
                  <option value="TechSupport">Tech Support</option>
                  <option value="General">General</option>
                </select>
              </div>
              <div className="form-group">
                <label>Content Description</label>
                <textarea 
                  value={newDocContent}
                  onChange={(e) => setNewDocContent(e.target.value)}
                  placeholder="Add the FAQ answers, guidelines, policies or lookup criteria details here. This text will be vectorized."
                  rows={6}
                  required
                />
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setShowNewDocModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary" disabled={isLoading}>
                  {isLoading ? 'Vector Indexing...' : 'Index Document (Embed)'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
