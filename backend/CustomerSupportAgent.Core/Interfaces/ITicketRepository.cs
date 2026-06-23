using System;
using System.Threading.Tasks;
using CustomerSupportAgent.Core.Models;

namespace CustomerSupportAgent.Core.Interfaces;

public interface ITicketRepository
{
    Task<Ticket?> GetTicketByIdAsync(Guid id);
    Task UpdateTicketAsync(Ticket ticket);
    Task AddAgentLogAsync(AgentLog log);
    Task SaveChangesAsync();
}
