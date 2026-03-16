using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Ugo.Orchestrator.Hubs;

namespace Ugo.Orchestrator.Core;

public sealed record McpToolCall(string ToolName, string ArgumentsJson, bool IsConflictSensitive = false);
public sealed record McpToolResult(string ToolName, string Output, bool Success);

public interface IMcpToolClient
{
    Task<McpToolResult> ExecuteToolAsync(McpToolCall call, CancellationToken cancellationToken = default);
}

public sealed class NoOpMcpToolClient : IMcpToolClient
{
    public async Task<McpToolResult> ExecuteToolAsync(McpToolCall call, CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken);
        return new McpToolResult(call.ToolName, $"Executed {call.ToolName} with {call.ArgumentsJson}", true);
    }
}

public sealed class FastExecutionEngine
{
    private readonly IMcpToolClient _mcpToolClient;
    private readonly IHubContext<AgentUgoHub> _hubContext;

    public FastExecutionEngine(IMcpToolClient mcpToolClient, IHubContext<AgentUgoHub> hubContext)
    {
        _mcpToolClient = mcpToolClient;
        _hubContext = hubContext;
    }

    public async Task<IReadOnlyList<McpToolResult>> ExecuteHyperSpeed(
        IReadOnlyList<McpToolCall> calls,
        CancellationToken cancellationToken = default)
    {
        if (calls.Count == 0)
        {
            return [];
        }

        var stopwatch = Stopwatch.StartNew();
        var results = new List<McpToolResult>(calls.Count);

        var parallelCalls = calls.Where(call => !call.IsConflictSensitive).ToArray();
        var serialCalls = calls.Where(call => call.IsConflictSensitive).ToArray();

        if (parallelCalls.Length > 0)
        {
            var tasks = parallelCalls.Select(call => _mcpToolClient.ExecuteToolAsync(call, cancellationToken));
            results.AddRange(await Task.WhenAll(tasks));
        }

        foreach (var call in serialCalls)
        {
            results.Add(await _mcpToolClient.ExecuteToolAsync(call, cancellationToken));
        }

        stopwatch.Stop();

        await _hubContext.Clients.All.SendAsync(
            "ReceiveThought",
            new AgentMessage(
                "Director",
                $"Parallel Execution of {calls.Count} tools complete in {stopwatch.ElapsedMilliseconds}ms",
                "Fast",
                DateTime.Now),
            cancellationToken);

        return results;
    }
}