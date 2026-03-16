using Microsoft.EntityFrameworkCore;

namespace Ugo.Orchestrator.Data;

/// <summary>
/// Local helper for persisting and loading durable agent snapshots from the project database.
/// </summary>
public sealed class DurableCheckpointStore
{
    private readonly UgoDbContext _dbContext;

    public DurableCheckpointStore(UgoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AgentStateSnapshot?> GetSnapshotAsync(Guid checkpointId, CancellationToken cancellationToken = default)
        => await _dbContext.AgentStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == checkpointId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Guid> SaveSnapshotAsync(AgentStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _dbContext.AgentStates.Add(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Id;
    }
}

