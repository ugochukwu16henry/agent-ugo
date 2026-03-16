using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Ugo.Orchestrator.Hubs;

namespace Ugo.Orchestrator.Services;

public sealed class TelemetryProvider
{
    public const string ActivitySourceName = "AgentUgo.Telemetry";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private readonly IHubContext<AgentUgoHub> _hubContext;
    private readonly ILogger<TelemetryProvider> _logger;

    public TelemetryProvider(IHubContext<AgentUgoHub> hubContext, ILogger<TelemetryProvider> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task TrackToolCallAsync(
        string toolName,
        string arguments,
        string status,
        string resultSummary,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("tool.call", ActivityKind.Internal);
        activity?.SetTag("agentugo.kind", "tool_call");
        activity?.SetTag("tool.name", toolName);
        activity?.SetTag("tool.arguments", arguments);
        activity?.SetTag("tool.status", status);
        activity?.SetTag("tool.result_summary", resultSummary);

        _logger.LogInformation("Tool call {ToolName} [{Status}] {ResultSummary}", toolName, status, resultSummary);

        await PublishInternalTraceAsync(
            new InternalTraceMessage(
                Kind: "ToolCall",
                Source: toolName,
                Content: $"Args={arguments} | Result={resultSummary}",
                Status: status,
                Timestamp: DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task TrackLlmThoughtAsync(
        string agentName,
        string thought,
        string status,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("llm.thought", ActivityKind.Internal);
        activity?.SetTag("agentugo.kind", "llm_thought");
        activity?.SetTag("llm.agent", agentName);
        activity?.SetTag("llm.status", status);
        activity?.SetTag("llm.thought", thought);

        _logger.LogInformation("LLM thought from {AgentName} [{Status}] {Thought}", agentName, status, thought);

        await PublishInternalTraceAsync(
            new InternalTraceMessage(
                Kind: "LlmThought",
                Source: agentName,
                Content: thought,
                Status: status,
                Timestamp: DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private Task PublishInternalTraceAsync(InternalTraceMessage trace, CancellationToken cancellationToken)
        => _hubContext.Clients.All.SendAsync("ReceiveInternalTrace", trace, cancellationToken);
}