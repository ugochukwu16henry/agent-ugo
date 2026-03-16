using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Ugo.Orchestrator;

public sealed record TaskEntry(
    string Id,
    string Description,
    string AssignedAgent,
    AgentRole Role,
    TaskLifecycleState Lifecycle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Simple in-memory task ledger used by the Director to coordinate work
/// between specialized agents. This can later be replaced with a durable
/// store (database, file, etc.) without changing the orchestration logic.
/// </summary>
public sealed class TaskLedger
{
    private readonly ConcurrentDictionary<string, TaskEntry> _tasks = new();

    public IEnumerable<TaskEntry> Tasks => _tasks.Values;

    public TaskEntry AddTask(string description, string assignedAgent, AgentRole role, TaskLifecycleState lifecycle = TaskLifecycleState.New)
    {
        var id = Guid.NewGuid().ToString("N");
        var entry = new TaskEntry(
            id,
            description,
            assignedAgent,
            role,
            lifecycle,
            DateTimeOffset.UtcNow,
            completedAt: null);

        _tasks[id] = entry;
        return entry;
    }

    public bool TryUpdateLifecycle(string id, TaskLifecycleState lifecycle)
    {
        if (!_tasks.TryGetValue(id, out var existing))
        {
            return false;
        }

        var updated = existing with
        {
            Lifecycle = lifecycle,
            CompletedAt = lifecycle is TaskLifecycleState.Completed or TaskLifecycleState.Failed
                ? DateTimeOffset.UtcNow
                : existing.CompletedAt
        };

        _tasks[id] = updated;
        return true;
    }
}

