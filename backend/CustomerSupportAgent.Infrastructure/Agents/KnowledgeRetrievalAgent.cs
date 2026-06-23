using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using CustomerSupportAgent.Core.Interfaces;
using CustomerSupportAgent.Core.Models;

namespace CustomerSupportAgent.Infrastructure.Agents;

public class KnowledgeRetrievalAgent : ISupportAgent
{
    private readonly Kernel _kernel;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IConfiguration _configuration;

    public KnowledgeRetrievalAgent(Kernel kernel, IKnowledgeBaseService knowledgeBase, IConfiguration configuration)
    {
        _kernel = kernel;
        _knowledgeBase = knowledgeBase;
        _configuration = configuration;
    }

    public string Name => "Knowledge Retrieval Agent";

    private bool IsDummyKey()
    {
        var key = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return string.IsNullOrWhiteSpace(key) || 
               key.Equals("dummy-key-for-initial-setup", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AgentLog> RunAsync(Ticket ticket, Dictionary<string, object>? context = null)
    {
        var log = new AgentLog
        {
            TicketId = ticket.Id,
            AgentName = Name,
            Action = "Formulating Query & RAG Retrieval",
            Input = $"Intent: {ticket.Intent}\nSubject: {ticket.Subject}",
            CreatedAt = DateTime.UtcNow
        };

        if (IsDummyKey())
        {
            // Offline / Mock Simulation Mode
            string optimizedQuery = ticket.Intent switch
            {
                "Refund" => "refund returns policy",
                "Shipping" => "shipping times and tracking",
                "TechSupport" => "website login troubleshooting",
                _ => ticket.Subject
            };

            log.Input += $"\nOptimized Search Query (Offline Simulation): {optimizedQuery}";

            var matchingDocs = await _knowledgeBase.SearchAsync(optimizedQuery, limit: 3);

            if (context != null)
            {
                context["retrieved_docs"] = matchingDocs;
                context["search_query"] = optimizedQuery;
            }

            var docsSummary = string.Join("\n\n", matchingDocs.Select(d => $"[Source: {d.Title}] ({d.Category})\n{d.Content}"));
            
            log.Output = matchingDocs.Count == 0 
                ? "No matching documents found in knowledge base." 
                : $"Retrieved {matchingDocs.Count} documents:\n\n{docsSummary}\n\n[OFFLINE SIMULATION LOG]: Retrieved document sources from local database using in-memory cosine similarity.";

            log.Status = "Success";
            return log;
        }

        var formulationPrompt = $$"""
            You are a Knowledge Retrieval Agent.
            Your job is to read the customer email and formulate a single search query to retrieve relevant documentation from a knowledge base.
            Focus on the core issue the user is reporting.
            Keep the search query short, concise, and focused on search keywords (e.g., "refund policy", "shipping delay tracker", "reset password login").

            Email Subject: {{ticket.Subject}}
            Email Body:
            {{ticket.Body}}

            Classified Intent: {{ticket.Intent}}

            Search Query:
            """;

        try
        {
            var queryResult = await _kernel.InvokePromptAsync<string>(formulationPrompt);
            var optimizedQuery = queryResult?.Trim() ?? ticket.Subject;

            log.Input += $"\nOptimized Search Query: {optimizedQuery}";

            var matchingDocs = await _knowledgeBase.SearchAsync(optimizedQuery, limit: 3);

            if (context != null)
            {
                context["retrieved_docs"] = matchingDocs;
                context["search_query"] = optimizedQuery;
            }

            var docsSummary = string.Join("\n\n", matchingDocs.Select(d => $"[Source: {d.Title}] ({d.Category})\n{d.Content}"));
            
            log.Output = matchingDocs.Count == 0 
                ? "No matching documents found in knowledge base." 
                : $"Retrieved {matchingDocs.Count} documents:\n\n{docsSummary}";

            log.Status = "Success";
        }
        catch (Exception ex)
        {
            log.Status = "Error";
            log.Output = $"Failed to retrieve knowledge: {ex.Message}";
        }

        return log;
    }
}
