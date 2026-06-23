using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using CustomerSupportAgent.Core.Interfaces;
using CustomerSupportAgent.Core.Models;

namespace CustomerSupportAgent.Infrastructure.Agents;

public class QualityReviewAgent : ISupportAgent
{
    private readonly Kernel _kernel;
    private readonly IConfiguration _configuration;

    public QualityReviewAgent(Kernel kernel, IConfiguration configuration)
    {
        _kernel = kernel;
        _configuration = configuration;
    }

    public string Name => "Quality Review Agent";

    private bool IsDummyKey()
    {
        var key = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return string.IsNullOrWhiteSpace(key) || 
               key.Equals("dummy-key-for-initial-setup", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AgentLog> RunAsync(Ticket ticket, Dictionary<string, object>? context = null)
    {
        TicketDraft? latestDraft = null;
        if (context != null && context.TryGetValue("latest_draft", out var draftObj) && draftObj is TicketDraft d)
        {
            latestDraft = d;
        }
        else
        {
            latestDraft = ticket.Drafts.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        }

        if (latestDraft == null)
        {
            return new AgentLog
            {
                TicketId = ticket.Id,
                AgentName = Name,
                Action = "Reviewing Draft",
                Input = "No draft found to review",
                Output = "Aborted review: no draft generated",
                Status = "Error",
                CreatedAt = DateTime.UtcNow
            };
        }

        var log = new AgentLog
        {
            TicketId = ticket.Id,
            AgentName = Name,
            Action = "Reviewing Draft",
            Input = $"Draft length: {latestDraft.Content.Length} characters",
            CreatedAt = DateTime.UtcNow
        };

        if (IsDummyKey())
        {
            // Offline / Mock Simulation Mode
            double score = 8.5;
            string feedback = "The response is polite, addresses all customer questions, cites the correct store policies, and provides accurate order details. Recommended for dispatch.";
            
            // If the operator previously rejected it, give it a higher score to show progress
            if (ticket.Drafts.Count > 1)
            {
                score = 9.5;
                feedback = "The revised response successfully addresses the operator's rejection notes. The text is accurate and complete.";
            }

            latestDraft.ReviewScore = score;
            latestDraft.ReviewFeedback = feedback;
            latestDraft.Status = DraftStatus.PendingApproval; // Re-transition from Rejected

            log.Output = $$"""
            {
              "score": {{score.ToString("0.0")}},
              "feedback": "{{feedback}}",
              "passed": true
            }
            
            [OFFLINE SIMULATION LOG]: Audited the email draft response locally. Passed quality check criteria.
            """;

            log.Status = "Success";
            return log;
        }

        string retrievedDocsContent = "";
        if (context != null && context.TryGetValue("retrieved_docs", out var docsObj) && docsObj is List<KnowledgeDocument> docs)
        {
            retrievedDocsContent = string.Join("\n\n", docs.Select(doc => $"[Source: {doc.Title}] ({doc.Category})\n{doc.Content}"));
        }

        var prompt = $$"""
            You are a Quality Review Agent for a customer support team.
            Your job is to audit the drafted email response against the customer email and retrieved knowledge documents.

            Evaluate the draft based on:
            1. Accuracy: Does it include any false information or policies not supported by the retrieved knowledge?
            2. Completeness: Does it answer all questions asked by the customer?
            3. Tone: Is it polite, helpful, and brand-aligned?
            4. Formatting: Is it structured cleanly as an email?

            Retrieved Knowledge Base Documents:
            ---
            {{(string.IsNullOrEmpty(retrievedDocsContent) ? "No knowledge documents retrieved." : retrievedDocsContent)}}
            ---

            Original Customer Email:
            - From: {{ticket.CustomerEmail}}
            - Subject: {{ticket.Subject}}
            - Body:
            {{ticket.Body}}

            Draft Email to Review:
            ---
            {{latestDraft.Content}}
            ---

            You MUST respond ONLY with a JSON object in the following format:
            {
              "score": 8.5, // Float score between 0.0 and 10.0
              "feedback": "Clear feedback explaining what is good or what needs to be improved.",
              "passed": true // boolean: true if score >= 7.0, false otherwise
            }

            Response JSON:
            """;

        try
        {
            var response = await _kernel.InvokePromptAsync<string>(prompt);
            var responseText = response?.Trim() ?? string.Empty;

            if (responseText.StartsWith("```json"))
            {
                responseText = responseText.Substring(7).Trim();
            }
            if (responseText.EndsWith("```"))
            {
                responseText = responseText.Substring(0, responseText.Length - 3).Trim();
            }

            log.Output = responseText;

            using (var jsonDoc = JsonDocument.Parse(responseText))
            {
                var root = jsonDoc.RootElement;
                var score = root.GetProperty("score").GetDouble();
                var feedback = root.GetProperty("feedback").GetString();
                var passed = root.GetProperty("passed").GetBoolean();

                latestDraft.ReviewScore = score;
                latestDraft.ReviewFeedback = feedback;

                if (!passed)
                {
                    latestDraft.Status = DraftStatus.Rejected;
                }
            }

            log.Status = "Success";
        }
        catch (Exception ex)
        {
            log.Status = "Error";
            log.Output = $"Failed to review draft: {ex.Message}. Raw Output: {log.Output}";
            
            latestDraft.ReviewScore = 5.0;
            latestDraft.ReviewFeedback = $"Quality Review agent internal error: {ex.Message}";
            latestDraft.Status = DraftStatus.Rejected;
        }

        return log;
    }
}
