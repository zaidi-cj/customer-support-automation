using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CustomerSupportAgent.Core.Interfaces;
using CustomerSupportAgent.Core.Models;

namespace CustomerSupportAgent.Core.Orchestrator;

public class WorkflowOrchestrator
{
    private readonly IEnumerable<ISupportAgent> _agents;
    private readonly ITicketRepository _repository;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    public WorkflowOrchestrator(
        IEnumerable<ISupportAgent> agents,
        ITicketRepository repository,
        ILogger<WorkflowOrchestrator> logger)
    {
        _agents = agents;
        _repository = repository;
        _logger = logger;
    }

    public async Task ProcessTicketAsync(Guid ticketId)
    {
        var ticket = await _repository.GetTicketByIdAsync(ticketId);
        if (ticket == null)
        {
            _logger.LogError("Ticket {TicketId} not found.", ticketId);
            return;
        }

        var context = new Dictionary<string, object>();

        try
        {
            // Step 1: Intent Classification
            ticket.Status = TicketStatus.Classifying;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateTicketAsync(ticket);
            await _repository.SaveChangesAsync();

            var classifier = GetAgent("Intent Classification Agent");
            var classLog = await classifier.RunAsync(ticket, context);
            await _repository.AddAgentLogAsync(classLog);
            await _repository.SaveChangesAsync();

            // Step 2: Knowledge Retrieval (RAG)
            ticket.Status = TicketStatus.Retrieving;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateTicketAsync(ticket);
            await _repository.SaveChangesAsync();

            var retriever = GetAgent("Knowledge Retrieval Agent");
            var retLog = await retriever.RunAsync(ticket, context);
            await _repository.AddAgentLogAsync(retLog);
            await _repository.SaveChangesAsync();

            // Step 3 & 4: Response Generation & Quality Review Loop (Retry up to 3 times if rejected)
            int attempt = 0;
            const int maxAttempts = 3;
            bool passedReview = false;

            var writer = GetAgent("Response Generation Agent");
            var reviewer = GetAgent("Quality Review Agent");

            while (attempt < maxAttempts && !passedReview)
            {
                attempt++;
                _logger.LogInformation("Generating response draft. Attempt {Attempt} of {MaxAttempts}", attempt, maxAttempts);

                ticket.Status = TicketStatus.Drafting;
                ticket.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateTicketAsync(ticket);
                await _repository.SaveChangesAsync();

                var writeLog = await writer.RunAsync(ticket, context);
                await _repository.AddAgentLogAsync(writeLog);
                await _repository.SaveChangesAsync();

                ticket.Status = TicketStatus.Reviewing;
                ticket.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateTicketAsync(ticket);
                await _repository.SaveChangesAsync();

                var reviewLog = await reviewer.RunAsync(ticket, context);
                await _repository.AddAgentLogAsync(reviewLog);
                await _repository.SaveChangesAsync();

                var latestDraft = ticket.Drafts.OrderByDescending(d => d.CreatedAt).FirstOrDefault();
                if (latestDraft != null && latestDraft.Status != DraftStatus.Rejected)
                {
                    passedReview = true;
                    latestDraft.Status = DraftStatus.PendingApproval;
                }
                else
                {
                    _logger.LogWarning("Draft rejected on attempt {Attempt}. Review Feedback: {Feedback}", attempt, latestDraft?.ReviewFeedback);
                }
            }

            // Step 5: Transition to Pending Operator Approval
            ticket.Status = TicketStatus.PendingApproval;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateTicketAsync(ticket);
            await _repository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ticket {TicketId}", ticketId);
            ticket.Status = TicketStatus.Failed;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateTicketAsync(ticket);

            var failLog = new AgentLog
            {
                TicketId = ticket.Id,
                AgentName = "Orchestrator",
                Action = "Orchestration Pipeline",
                Input = $"Processing ticket: {ticketId}",
                Output = $"Workflow execution encountered an error: {ex.Message}",
                Status = "Error",
                CreatedAt = DateTime.UtcNow
            };
            await _repository.AddAgentLogAsync(failLog);
            await _repository.SaveChangesAsync();
        }
    }

    private ISupportAgent GetAgent(string name)
    {
        var agent = _agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (agent == null)
        {
            throw new InvalidOperationException($"Required agent '{name}' is not registered.");
        }
        return agent;
    }
}
