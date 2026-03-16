using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Ugo.Orchestrator.Services;

namespace Ugo.Orchestrator.Hubs;

public record AgentMessage(string AgentName, string Thought, string Status, DateTime Timestamp);
public record ApprovalRequestMessage(string Id, string Action, string Parameters, string Reason, DateTimeOffset RequestedAt);
public record ApprovalDecisionMessage(string ApprovalId, bool Approved, DateTimeOffset DecidedAt);
public record InternalTraceMessage(string Kind, string Source, string Content, string Status, DateTimeOffset Timestamp);
public record PreviewFrameMessage(string Base64Png, string SourceUrl, DateTimeOffset CapturedAt, string Note);

public class AgentUgoHub : Hub
{
    private readonly OrchestrationService _orchestrationService;

    public AgentUgoHub(OrchestrationService orchestrationService)
    {
        _orchestrationService = orchestrationService;
    }

    /// <summary>
    /// High-speed relay for parallel agent updates to all connected clients.
    /// </summary>
    public async Task BroadcastThought(AgentMessage message)
        => await Clients.All.SendAsync("ReceiveThought", message);

    public async Task BroadcastInternalTrace(InternalTraceMessage trace)
        => await Clients.All.SendAsync("ReceiveInternalTrace", trace);

    public async Task BroadcastPreviewFrame(PreviewFrameMessage frame)
        => await Clients.All.SendAsync("ReceivePreviewFrame", frame);

    public Task SubmitApprovalDecision(string approvalId, bool approved)
        => _orchestrationService.UserDecisionReceivedAsync(approvalId, approved);
}

