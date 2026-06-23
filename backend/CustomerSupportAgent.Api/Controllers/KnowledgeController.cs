using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CustomerSupportAgent.Core.Interfaces;
using CustomerSupportAgent.Core.Models;
using CustomerSupportAgent.Infrastructure.Data;

namespace CustomerSupportAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KnowledgeController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IKnowledgeBaseService _knowledgeBaseService;

    public KnowledgeController(AppDbContext dbContext, IKnowledgeBaseService knowledgeBaseService)
    {
        _dbContext = dbContext;
        _knowledgeBaseService = knowledgeBaseService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<KnowledgeDocument>>> GetDocuments()
    {
        var docs = await _dbContext.KnowledgeDocuments
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new KnowledgeDocument
            {
                Id = d.Id,
                Title = d.Title,
                Content = d.Content,
                Category = d.Category,
                CreatedAt = d.CreatedAt
                // Exclude embedding from response to keep payload small
            })
            .ToListAsync();

        return Ok(docs);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDocument([FromBody] CreateDocRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || 
            string.IsNullOrWhiteSpace(request.Content) || 
            string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest("Title, Content, and Category are required.");
        }

        try
        {
            await _knowledgeBaseService.IndexDocumentAsync(request.Title.Trim(), request.Content.Trim(), request.Category.Trim());
            return Ok(new { message = "Document indexed successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal error during vector indexing: {ex.Message}");
        }
    }
}

public record CreateDocRequest(string Title, string Content, string Category);
