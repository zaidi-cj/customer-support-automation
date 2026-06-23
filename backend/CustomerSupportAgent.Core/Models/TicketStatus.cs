namespace CustomerSupportAgent.Core.Models;

public enum TicketStatus
{
    Received,
    Classifying,
    Retrieving,
    Drafting,
    Reviewing,
    PendingApproval,
    Approved,
    Rejected,
    Sending,
    Sent,
    Failed
}
