using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.Configuration;
using CustomerSupportAgent.Core.Interfaces;
using CustomerSupportAgent.Core.Models;
using CustomerSupportAgent.Infrastructure.Data;

namespace CustomerSupportAgent.Infrastructure.Services;

public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly AppDbContext _dbContext;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IConfiguration _configuration;

    public KnowledgeBaseService(
        AppDbContext dbContext, 
        ITextEmbeddingGenerationService embeddingService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _configuration = configuration;
    }

    private bool IsDummyKey()
    {
        var key = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return string.IsNullOrWhiteSpace(key) || 
               key.Equals("dummy-key-for-initial-setup", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    public async Task IndexDocumentAsync(string title, string content, string category)
    {
        float[] embeddingArray;

        if (IsDummyKey())
        {
            // Seed a deterministic float array based on the text hash for offline testing
            var rng = new Random(content.GetHashCode());
            embeddingArray = Enumerable.Range(0, 1536).Select(_ => (float)rng.NextDouble()).ToArray();
        }
        else
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(content);
            embeddingArray = embedding.ToArray();
        }
        
        var doc = new KnowledgeDocument
        {
            Title = title,
            Content = content,
            Category = category,
            Embedding = embeddingArray
        };

        _dbContext.KnowledgeDocuments.Add(doc);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<KnowledgeDocument>> SearchAsync(string query, int limit = 3)
    {
        float[] floatArray;

        if (IsDummyKey())
        {
            var rng = new Random(query.GetHashCode());
            floatArray = Enumerable.Range(0, 1536).Select(_ => (float)rng.NextDouble()).ToArray();
        }
        else
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(query);
            floatArray = embedding.ToArray();
        }

        bool isPostgres = _dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        if (isPostgres)
        {
            var vectorString = "[" + string.Join(",", floatArray) + "]";
            return await _dbContext.KnowledgeDocuments
                .FromSqlRaw("SELECT * FROM knowledge_documents ORDER BY embedding <=> {0}::vector LIMIT {1}", vectorString, limit)
                .ToListAsync();
        }
        else
        {
            var allDocs = await _dbContext.KnowledgeDocuments.ToListAsync();
            
            return allDocs
                .Select(doc => new { Document = doc, Similarity = ComputeCosineSimilarity(floatArray, doc.Embedding) })
                .OrderByDescending(x => x.Similarity)
                .Take(limit)
                .Select(x => x.Document)
                .ToList();
        }
    }

    private static double ComputeCosineSimilarity(float[] vector1, float[]? vector2)
    {
        if (vector2 == null || vector1.Length != vector2.Length)
            return 0;

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            normA += Math.Pow(vector1[i], 2);
            normB += Math.Pow(vector2[i], 2);
        }

        if (normA == 0.0 || normB == 0.0)
            return 0.0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
