using System;
using System.Collections.Generic;

namespace CustomerSupportAgent.Core.Models;

public class Ticket
{
    public Guid Id { get; set; } // Let EF Core generate Guids on insert
    public string CustomerEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Received;
    
    public string? Intent { get; set; }
    public string? Category { get; set; }
    public string? MetadataJson { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<AgentLog> Logs { get; set; } = new List<AgentLog>();
    public virtual ICollection<TicketDraft> Drafts { get; set; } = new List<TicketDraft>();
}
