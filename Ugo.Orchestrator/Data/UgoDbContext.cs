using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ugo.Orchestrator.Data;

public sealed class UgoDbContext : DbContext
{
    public UgoDbContext(DbContextOptions<UgoDbContext> options)
        : base(options)
    {
    }

    public DbSet<AgentStateSnapshot> AgentStates => Set<AgentStateSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentStateSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CheckpointType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ConversationJson).IsRequired();

            // Map Variables dictionary to a JSON column (TEXT in SQLite, JSONB in PostgreSQL).
            entity.Property(e => e.Variables)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                      v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new());
        });
    }
}

public sealed class AgentStateSnapshot
{
    public Guid Id { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string CheckpointType { get; set; } = string.Empty;

    public string ConversationJson { get; set; } = string.Empty;

    /// <summary>
    /// Arbitrary, schema-less key/value store for agent variables.
    /// Backed by a JSON/JSONB column.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new();

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

