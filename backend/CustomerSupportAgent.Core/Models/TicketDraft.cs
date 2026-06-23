using System;
using System.Text.Json.Serialization;

namespace CustomerSupportAgent.Core.Models;

public class TicketDraft
{
    public Guid Id { get; set; } // Let EF Core generate Guids on insert
    public Guid TicketId { get; set; }
    public string Content { get; set; } = string.Empty;
    public double ReviewScore { get; set; }
    public string? ReviewFeedback { get; set; }
    public string? OperatorComments { get; set; }
    public DraftStatus Status { get; set; } = DraftStatus.PendingApproval;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActionedAt { get; set; }

    [JsonIgnore]
    public virtual Ticket? Ticket { get; set; }
}
