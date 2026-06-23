using System.Collections.Generic;
using System.Threading.Tasks;
using CustomerSupportAgent.Core.Models;

namespace CustomerSupportAgent.Core.Interfaces;

public interface IKnowledgeBaseService
{
    Task IndexDocumentAsync(string title, string content, string category);
    Task<List<KnowledgeDocument>> SearchAsync(string query, int limit = 3);
}
