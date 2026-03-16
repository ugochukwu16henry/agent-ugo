using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ugo.Orchestrator.Data;
using Ugo.Orchestrator.Hubs;

namespace Ugo.Orchestrator.Memory;

public sealed record TimeTravelCheckpointView(
    Guid Id,
    string ThreadId,
    string AgentName,
    string NodeName,
    string LastThought,
    string? ManualOverride,
    DateTimeOffset CreatedAtUtc);

internal sealed record TrackedTimelineState(
    string ThreadId,
    string AgentName,
    string NodeName,
    string ConversationJson,
    Dictionary<string, string> Variables,
    string SerializedSessionJson);

public sealed class TimeTravelService
{
    private const string AgentSessionJsonKey = "AgentSessionJson";
    private readonly IDbContextFactory<UgoDbContext> _dbContextFactory;
    private readonly IHubContext<AgentUgoHub> _hubContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ConcurrentDictionary<string, TrackedTimelineState> _trackedStates = new(StringComparer.OrdinalIgnoreCase);

    public TimeTravelService(
        IDbContextFactory<UgoDbContext> dbContextFactory,
        IHubContext<AgentUgoHub> hubContext,
        IServiceScopeFactory serviceScopeFactory)
    {
        _dbContextFactory = dbContextFactory;
        _hubContext = hubContext;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task RecordStateAsync(
        string threadId,
        string agentName,
        string nodeName,
        string lastThought,
        IReadOnlyDictionary<string, object> values,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var variables = values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        variables["ThreadId"] = threadId;
        variables["AgentName"] = agentName;
        variables["NodeName"] = nodeName;
        variables["LastThought"] = lastThought;
        variables[AgentSessionJsonKey] = BuildSessionEnvelopeJson(threadId, nodeName, variables);

        var state = new TrackedTimelineState(
            threadId,
            agentName,
            nodeName,
            JsonSerializer.Serialize(new[]
            {
                new
                {
                    agentName,
                    nodeName,
                    lastThought,
                    recordedAtUtc = DateTimeOffset.UtcNow
                }
            }),
            variables,
            variables[AgentSessionJsonKey]);

        _trackedStates[threadId] = state;
        return Task.CompletedTask;
    }

    public async Task<Guid> CreateCheckpointAsync(string threadId, string nodeName, CancellationToken cancellationToken = default)
    {
        if (!_trackedStates.TryGetValue(threadId, out var state))
        {
            throw new InvalidOperationException($"No tracked state exists for thread '{threadId}'.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var snapshot = new AgentStateSnapshot
        {
            Id = Guid.NewGuid(),
            AgentName = state.AgentName,
            CheckpointType = nodeName,
            ConversationJson = state.ConversationJson,
            Variables = new Dictionary<string, string>(state.Variables, StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.AgentStates.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync(
            "ReceiveInternalTrace",
            new InternalTraceMessage(
                "Checkpoint",
                state.AgentName,
                $"Checkpoint '{nodeName}' saved for thread {threadId}.",
                "Checkpointed",
                snapshot.CreatedAtUtc),
            cancellationToken);

        return snapshot.Id;
    }

    public async Task<TimeTravelCheckpointView?> RewindToStep(Guid checkpointId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var snapshot = await dbContext.AgentStates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == checkpointId, cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        var threadId = snapshot.Variables.GetValueOrDefault("ThreadId", checkpointId.ToString("N"));
        var nodeName = snapshot.Variables.GetValueOrDefault("NodeName", snapshot.CheckpointType);

        _trackedStates[threadId] = new TrackedTimelineState(
            threadId,
            snapshot.AgentName,
            nodeName,
            snapshot.ConversationJson,
            new Dictionary<string, string>(snapshot.Variables, StringComparer.OrdinalIgnoreCase),
            snapshot.Variables.GetValueOrDefault(AgentSessionJsonKey, BuildSessionEnvelopeJson(threadId, nodeName, snapshot.Variables)));

        await _hubContext.Clients.All.SendAsync(
            "ReceiveThought",
            new AgentMessage("Director", $"Rewound timeline to checkpoint {checkpointId:N} at {nodeName}.", "Rewound", DateTime.Now),
            cancellationToken);

        return ToView(snapshot);
    }

    public async Task<TimeTravelCheckpointView?> ModifyAndResume(Guid checkpointId, string newInstruction, CancellationToken cancellationToken = default)
    {
        var rewound = await RewindToStep(checkpointId, cancellationToken);
        if (rewound is null)
        {
            return null;
        }

        if (!_trackedStates.TryGetValue(rewound.ThreadId, out var trackedState))
        {
            return null;
        }

        var forkThreadId = Guid.NewGuid().ToString("N");
        var variables = new Dictionary<string, string>(trackedState.Variables, StringComparer.OrdinalIgnoreCase)
        {
            ["ThreadId"] = forkThreadId,
            ["ManualOverride"] = newInstruction,
            ["LastThought"] = $"[HUMAN OVERRIDE]: {newInstruction}",
            ["ForkedFromCheckpointId"] = checkpointId.ToString("N")
        };

        var serializedSessionJson = BuildSessionEnvelopeJson(forkThreadId, $"{trackedState.NodeName}.Fork", variables);
        variables[AgentSessionJsonKey] = serializedSessionJson;

        _trackedStates[forkThreadId] = trackedState with
        {
            ThreadId = forkThreadId,
            NodeName = $"{trackedState.NodeName}.Fork",
            Variables = variables,
            SerializedSessionJson = serializedSessionJson
        };

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var forkSnapshot = new AgentStateSnapshot
        {
            Id = Guid.NewGuid(),
            AgentName = trackedState.AgentName,
            CheckpointType = "ManualOverrideFork",
            ConversationJson = trackedState.ConversationJson,
            Variables = variables,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.AgentStates.Add(forkSnapshot);
        await dbContext.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync(
            "ReceiveThought",
            new AgentMessage("Director", $"Manual override applied. Forked checkpoint {checkpointId:N} into thread {forkThreadId}.", "Forked", DateTime.Now),
            cancellationToken);

        var goal = variables.GetValueOrDefault("Goal");
        if (!string.IsNullOrWhiteSpace(goal))
        {
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var director = scope.ServiceProvider.GetRequiredService<DirectorOrchestrator>();
                await director.RunParallelDevTaskAsync($"{goal} [MANUAL OVERRIDE: {newInstruction}]");
            }, cancellationToken);
        }

        return ToView(forkSnapshot);
    }

    public async Task<IReadOnlyList<TimeTravelCheckpointView>> GetHistoryAsync(int limit = 30, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var snapshots = await dbContext.AgentStates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return snapshots
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(limit)
            .Select(ToView)
            .ToArray();
    }

    private static string BuildSessionEnvelopeJson(string threadId, string nodeName, IReadOnlyDictionary<string, string> variables)
        => JsonSerializer.Serialize(new
        {
            SessionType = "AgentSessionEnvelope",
            ThreadId = threadId,
            NodeName = nodeName,
            State = variables,
            StoredAtUtc = DateTimeOffset.UtcNow
        });

    private static TimeTravelCheckpointView ToView(AgentStateSnapshot snapshot)
        => new(
            snapshot.Id,
            snapshot.Variables.GetValueOrDefault("ThreadId", snapshot.Id.ToString("N")),
            snapshot.AgentName,
            snapshot.Variables.GetValueOrDefault("NodeName", snapshot.CheckpointType),
            snapshot.Variables.GetValueOrDefault("LastThought", "No thought captured."),
            snapshot.Variables.GetValueOrDefault("ManualOverride"),
            snapshot.CreatedAtUtc);
}
