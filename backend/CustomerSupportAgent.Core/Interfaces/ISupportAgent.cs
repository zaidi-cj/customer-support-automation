using System.Collections.Generic;
using System.Threading.Tasks;
using CustomerSupportAgent.Core.Models;

namespace CustomerSupportAgent.Core.Interfaces;

public interface ISupportAgent
{
    string Name { get; }
    Task<AgentLog> RunAsync(Ticket ticket, Dictionary<string, object>? context = null);
}
