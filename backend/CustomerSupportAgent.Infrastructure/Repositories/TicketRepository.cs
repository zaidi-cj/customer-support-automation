using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CustomerSupportAgent.Core.Interfaces;
using CustomerSupportAgent.Core.Models;
using CustomerSupportAgent.Infrastructure.Data;

namespace CustomerSupportAgent.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly AppDbContext _dbContext;

    public TicketRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Ticket?> GetTicketByIdAsync(Guid id)
    {
        return await _dbContext.Tickets
            .Include(t => t.Logs)
            .Include(t => t.Drafts)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public Task UpdateTicketAsync(Ticket ticket)
    {
        // No-op: EF Core's change tracker automatically monitors changes to tracked entities.
        // Manually setting Entry.State to Modified causes EF Core to treat newly added collection elements (with pre-assigned Guids) as existing database rows, triggering DbUpdateConcurrencyExceptions.
        return Task.CompletedTask;
    }

    public async Task AddAgentLogAsync(AgentLog log)
    {
        await _dbContext.AgentLogs.AddAsync(log);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
