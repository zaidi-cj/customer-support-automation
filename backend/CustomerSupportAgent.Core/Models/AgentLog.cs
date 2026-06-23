using System;
using System.Text.Json.Serialization;

namespace CustomerSupportAgent.Core.Models;

public class AgentLog
{
    public Guid Id { get; set; } // Let EF Core generate Guids on insert
    public Guid TicketId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string Status { get; set; } = "Success"; // Success, Error
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public virtual Ticket? Ticket { get; set; }
}
