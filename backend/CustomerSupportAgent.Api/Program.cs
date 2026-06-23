using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using CustomerSupportAgent.Core.Interfaces;
using CustomerSupportAgent.Core.Models;
using CustomerSupportAgent.Core.Orchestrator;
using CustomerSupportAgent.Infrastructure.Data;
using CustomerSupportAgent.Infrastructure.Repositories;
using CustomerSupportAgent.Infrastructure.Services;
using CustomerSupportAgent.Infrastructure.Agents;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Database Connection: Auto-Detect PostgreSQL vs SQLite Fallback
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Port=5432;Database=customersupport;Username=postgres;Password=postgrespassword;";

bool usePostgres = false;
try
{
    var connStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Timeout = 2,
        CommandTimeout = 2
    };
    using var conn = new NpgsqlConnection(connStringBuilder.ConnectionString);
    conn.Open();
    usePostgres = true;
    Console.WriteLine("Successfully connected to PostgreSQL. Using pgvector vector database storage.");
}
catch
{
    Console.WriteLine("PostgreSQL server is offline. Automatically falling back to local SQLite storage (customersupport.db) with local cosine similarity RAG calculation.");
}

if (usePostgres)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString, x => x.MigrationsAssembly("CustomerSupportAgent.Infrastructure")));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=customersupport.db", x => x.MigrationsAssembly("CustomerSupportAgent.Infrastructure")));
}

// Read and validate OpenAI API Key
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
}
if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    openAiApiKey = "dummy-key-for-initial-setup";
}

// Configure Semantic Kernel Text Embedding Generation
builder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
{
    var embeddingModelId = builder.Configuration["OpenAI:EmbeddingModelId"] ?? "text-embedding-3-small";
    return new OpenAITextEmbeddingGenerationService(embeddingModelId, openAiApiKey);
});

// Configure Semantic Kernel Chat Completion & Main Kernel Service
builder.Services.AddTransient<Kernel>(sp =>
{
    var modelId = builder.Configuration["OpenAI:ModelId"] ?? "gpt-4o";
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(modelId, openAiApiKey);
    return kernelBuilder.Build();
});

// Core & Infrastructure Services Wiring
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
builder.Services.AddScoped<WorkflowOrchestrator>();

// Register Support Agents
builder.Services.AddScoped<ISupportAgent, IntentClassificationAgent>();
builder.Services.AddScoped<ISupportAgent, KnowledgeRetrievalAgent>();
builder.Services.AddScoped<ISupportAgent, ResponseGenerationAgent>();
builder.Services.AddScoped<ISupportAgent, QualityReviewAgent>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// Apply migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = (AppDbContext)services.GetService(typeof(AppDbContext))!;
        context.Database.EnsureCreated();
        
        // Custom Seed Method to load base knowledge docs
        await SeedDataAsync(services);
    }
    catch (Exception ex)
    {
        var logger = (ILogger<Program>)services.GetService(typeof(ILogger<Program>))!;
        logger.LogError(ex, "An error occurred during database initialization or seeding.");
    }
}

app.Run();

// Database Seeding Helper
async Task SeedDataAsync(IServiceProvider services)
{
    var context = (AppDbContext)services.GetService(typeof(AppDbContext))!;
    var knowledgeBase = (IKnowledgeBaseService)services.GetService(typeof(IKnowledgeBaseService))!;

    // 1. Seed Knowledge Base Documents if empty
    if (!context.KnowledgeDocuments.Any())
    {
        var docs = new List<(string Title, string Content, string Category)>
        {
            ("Refund and Returns Policy", 
             "Customers are eligible for a full refund if requested within 30 days of the purchase date. The items must be unused and in their original packaging. Return shipping is free of charge for orders over $50. Once returned and inspected, the refund will be credited to the original payment method within 5-10 business days. After 30 days, we only offer store credit.",
             "Refund"),
             
            ("Shipping Times and Tracking", 
             "We offer Standard shipping (3-5 business days) and Express shipping (1-2 business days). Standard shipping is free for orders above $50. All orders include a tracking number which becomes active within 24 hours of dispatch. If a delivery status is marked as 'Delivered' but the customer has not received the package, they should wait 24 hours or contact the shipping carrier before submitting a lost package claim.",
             "Shipping"),
             
            ("Website Login Troubleshooting", 
             "If a customer is experiencing login issues or seeing password errors: 1. Advise them to use the 'Forgot Password' link to trigger a secure reset email. 2. Clear browser cache and cookies. 3. Try an Incognito window. 4. If account is locked after 5 failed login attempts, it will automatically unlock after 15 minutes. Support agents cannot manually override lockouts.",
             "TechSupport"),
             
            ("Contacting Human Escalation", 
             "If a customer demands to speak to a manager, or is extremely upset, agent responses should offer direct escalation. Express deep empathy, outline that a supervisor will contact them within 4 hours, and flag the ticket status for manual supervisor review.",
             "General")
        };

        foreach (var doc in docs)
        {
            await knowledgeBase.IndexDocumentAsync(doc.Title, doc.Content, doc.Category);
        }
        Console.WriteLine("Knowledge base seeded with 4 documents.");
    }

    // 2. Seed Mock Tickets if empty
    if (!context.Tickets.Any())
    {
        var ticket = new Ticket
        {
            CustomerEmail = "jane.doe@gmail.com",
            Subject = "Need help tracking order 10293",
            Body = "Hi, I placed an order last week (order number 10293). It says shipped on my profile, but I haven't received it yet and don't know where it is. Can you check where it is and when it will arrive? Thanks!",
            Status = TicketStatus.Received,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        Console.WriteLine("Mock tickets seeded.");
    }
}
