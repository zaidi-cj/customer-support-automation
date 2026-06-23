using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using CustomerSupportAgent.Core.Models;

namespace CustomerSupportAgent.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<AgentLog> AgentLogs => Set<AgentLog>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<TicketDraft> TicketDrafts => Set<TicketDraft>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        bool isPostgres = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        if (isPostgres)
        {
            // Enable pgvector extension
            modelBuilder.HasPostgresExtension("vector");
        }

        // Ticket Configuration
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("tickets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerEmail).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Intent).HasMaxLength(100);
            entity.Property(e => e.Category).HasMaxLength(100);
            
            if (isPostgres)
            {
                entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            }
            else
            {
                entity.Property(e => e.MetadataJson).HasColumnType("TEXT");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");
            }

            entity.HasMany(e => e.Logs)
                  .WithOne(e => e.Ticket)
                  .HasForeignKey(e => e.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Drafts)
                  .WithOne(e => e.Ticket)
                  .HasForeignKey(e => e.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AgentLog Configuration
        modelBuilder.Entity<AgentLog>(entity =>
        {
            entity.ToTable("agent_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Input).IsRequired();
            entity.Property(e => e.Output).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            
            if (isPostgres)
            {
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            }
            else
            {
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            }
        });

        // KnowledgeDocument Configuration
        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("knowledge_documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            
            if (isPostgres)
            {
                entity.Property(e => e.Embedding)
                      .HasColumnType("vector(1536)");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            }
            else
            {
                // SQLite Value Converter: store float[] as a comma-separated text column
                entity.Property(e => e.Embedding)
                      .HasColumnType("TEXT")
                      .HasConversion(
                          v => v == null ? null : string.Join(",", v),
                          v => string.IsNullOrEmpty(v) ? null : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(float.Parse).ToArray()
                      );
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            }
        });

        // TicketDraft Configuration
        modelBuilder.Entity<TicketDraft>(entity =>
        {
            entity.ToTable("ticket_drafts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            
            if (isPostgres)
            {
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            }
            else
            {
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            }
        });
    }
}
