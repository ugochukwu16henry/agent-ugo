using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Ugo.Orchestrator.Hubs;

namespace Ugo.Orchestrator.Services;

public sealed class OrchestrationService
{
    private readonly IHubContext<AgentUgoHub> _hubContext;
    private readonly TelemetryProvider _telemetryProvider;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingApprovals = new();

    public OrchestrationService(IHubContext<AgentUgoHub> hubContext, TelemetryProvider telemetryProvider)
    {
        _hubContext = hubContext;
        _telemetryProvider = telemetryProvider;
    }

    public async Task ExecuteTaskWithSafety(string userGoal, CancellationToken cancellationToken = default)
    {
        await PublishThoughtAsync("Director", $"Starting guarded execution for: {userGoal}", "Working", cancellationToken);

        const string actionName = "save_changes";

        if (!UgoSecurityPolicy.RequiresApproval(actionName))
        {
            await PublishThoughtAsync("Director", "No approval gate was required for this action.", "Success", cancellationToken);
            return;
        }

        var approved = await RequestApprovalAsync(
            action: actionName,
            parameters: userGoal,
            reason: "Agent Ugo wants to modify the codebase and needs explicit human approval.",
            cancellationToken: cancellationToken);

        await _telemetryProvider.TrackToolCallAsync(
            toolName: actionName,
            arguments: userGoal,
            status: approved ? "Approved" : "Rejected",
            resultSummary: approved ? "Human approved the critical tool call." : "Human rejected the critical tool call.",
            cancellationToken: cancellationToken);

        await PublishThoughtAsync(
            "Director",
            approved ? "Critical action approved. Resuming agent execution." : "Critical action rejected. Agent execution halted.",
            approved ? "Success" : "Rejected",
            cancellationToken);
    }

    public async Task<bool> RequestApprovalAsync(
        string action,
        string parameters,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var approvalId = Guid.NewGuid().ToString("N");
        var decisionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pendingApprovals.TryAdd(approvalId, decisionSource))
        {
            throw new InvalidOperationException($"Unable to register approval request '{approvalId}'.");
        }

        var request = new ApprovalRequestMessage(
            approvalId,
            action,
            parameters,
            reason,
            DateTimeOffset.UtcNow);

        await _telemetryProvider.TrackToolCallAsync(
            toolName: action,
            arguments: parameters,
            status: "Awaiting Approval",
            resultSummary: reason,
            cancellationToken: cancellationToken);

        await _hubContext.Clients.All.SendAsync("ApprovalNeeded", request, cancellationToken);

        using var cancellationRegistration = cancellationToken.Register(() => decisionSource.TrySetCanceled(cancellationToken));

        try
        {
            return await decisionSource.Task;
        }
        finally
        {
            _pendingApprovals.TryRemove(approvalId, out _);
        }
    }

    public async Task UserDecisionReceivedAsync(string approvalId, bool approved, CancellationToken cancellationToken = default)
    {
        if (!_pendingApprovals.TryGetValue(approvalId, out var decisionSource))
        {
            return;
        }

        decisionSource.TrySetResult(approved);

        var resolution = new ApprovalDecisionMessage(approvalId, approved, DateTimeOffset.UtcNow);
        await _hubContext.Clients.All.SendAsync("ApprovalResolved", resolution, cancellationToken);
        await _telemetryProvider.TrackToolCallAsync(
            toolName: "approval_decision",
            arguments: approvalId,
            status: approved ? "Approved" : "Rejected",
            resultSummary: approved ? "Human reviewer approved the pending action." : "Human reviewer rejected the pending action.",
            cancellationToken: cancellationToken);
        await PublishThoughtAsync(
            "Director",
            approved ? "Critical action approved by human reviewer." : "Critical action rejected by human reviewer.",
            approved ? "Success" : "Rejected",
            cancellationToken);
    }

    private async Task PublishThoughtAsync(string agentName, string thought, string status, CancellationToken cancellationToken)
    {
        await _telemetryProvider.TrackLlmThoughtAsync(agentName, thought, status, cancellationToken);

        await _hubContext.Clients.All.SendAsync(
            "ReceiveThought",
            new AgentMessage(agentName, thought, status, DateTime.Now),
            cancellationToken);
    }
}