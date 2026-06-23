using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CustomerSupportAgent.Core.Models;
using CustomerSupportAgent.Core.Orchestrator;
using CustomerSupportAgent.Infrastructure.Data;

namespace CustomerSupportAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IServiceScopeFactory _scopeFactory;

    public TicketsController(AppDbContext dbContext, IServiceScopeFactory scopeFactory)
    {
        _dbContext = dbContext;
        _scopeFactory = scopeFactory;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Ticket>>> GetTickets()
    {
        var tickets = await _dbContext.Tickets
            .Include(t => t.Drafts)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return Ok(tickets);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Ticket>> GetTicket(Guid id)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Logs)
            .Include(t => t.Drafts)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
        {
            return NotFound($"Ticket {id} not found.");
        }

        return Ok(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<Ticket>> CreateTicket([FromBody] CreateTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerEmail) || 
            string.IsNullOrWhiteSpace(request.Subject) || 
            string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest("CustomerEmail, Subject, and Body are required.");
        }

        var ticket = new Ticket
        {
            CustomerEmail = request.CustomerEmail.Trim(),
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim(),
            Status = TicketStatus.Received,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        // Create a new DI scope for the background task to avoid disposed DbContext exceptions
        _ = Task.Run(async () =>
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                try
                {
                    var orchestrator = (WorkflowOrchestrator)scope.ServiceProvider.GetService(typeof(WorkflowOrchestrator))!;
                    await orchestrator.ProcessTicketAsync(ticket.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error running background orchestrator for ticket {ticket.Id}: {ex.Message}");
                }
            }
        });

        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, ticket);
    }

    [HttpPost("{id}/run")]
    public async Task<IActionResult> RunWorkflow(Guid id)
    {
        var ticket = await _dbContext.Tickets.FindAsync(id);
        if (ticket == null)
        {
            return NotFound($"Ticket {id} not found.");
        }

        // Restart workflow in a background scope
        _ = Task.Run(async () =>
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                try
                {
                    var orchestrator = (WorkflowOrchestrator)scope.ServiceProvider.GetService(typeof(WorkflowOrchestrator))!;
                    await orchestrator.ProcessTicketAsync(ticket.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error running background orchestrator for ticket {ticket.Id}: {ex.Message}");
                }
            }
        });

        return Accepted();
    }
}

public record CreateTicketRequest(string CustomerEmail, string Subject, string Body);
