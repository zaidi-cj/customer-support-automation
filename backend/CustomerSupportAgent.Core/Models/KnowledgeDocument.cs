using System;

namespace CustomerSupportAgent.Core.Models;

public class KnowledgeDocument
{
    public Guid Id { get; set; } // Let EF Core generate Guids on insert
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
