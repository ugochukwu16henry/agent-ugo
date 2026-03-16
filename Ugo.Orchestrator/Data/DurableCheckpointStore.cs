using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ugo.Orchestrator.Data;

/// <summary>
/// Durable checkpoint store that persists agent conversation and internal state
/// using Entity Framework Core and a JSON/JSONB-backed variables column.
/// </summary>
public sealed class DurableCheckpointStore : ICheckpointStore
{
    private readonly UgoDbContext _dbContext;

    public DurableCheckpointStore(UgoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Checkpoint?> GetCheckpointAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(checkpointId, out var id))
        {
            return null;
        }

        var snapshot = await _dbContext.AgentStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null)
        {
            return null;
        }

        var history = JsonSerializer.Deserialize<IList<ChatMessage>>(snapshot.ConversationJson) ?? new List<ChatMessage>();

        return new Checkpoint(
            Id: snapshot.Id.ToString("N"),
            Type: snapshot.CheckpointType,
            AgentName: snapshot.AgentName,
            ConversationHistory: history,
            Variables: snapshot.Variables.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value));
    }

    public async Task<string> SaveCheckpointAsync(Checkpoint checkpoint, CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrWhiteSpace(checkpoint.Id)
            ? Guid.NewGuid()
            : Guid.TryParse(checkpoint.Id, out var parsed) ? parsed : Guid.NewGuid();

        var conversationJson = JsonSerializer.Serialize(checkpoint.ConversationHistory);

        var snapshot = new AgentStateSnapshot
        {
            Id = id,
            AgentName = checkpoint.AgentName ?? "Unknown",
            CheckpointType = checkpoint.Type ?? "Default",
            ConversationJson = conversationJson,
            Variables = checkpoint.Variables?.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value?.ToString() ?? string.Empty) ?? new()
        };

        _dbContext.AgentStates.Add(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return snapshot.Id.ToString("N");
    }
}

