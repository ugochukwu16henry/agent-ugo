using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace Ugo.Orchestrator;

public sealed class DirectorOrchestrator
{
    private readonly AgentProfile _researcherAgent;
    private readonly AgentProfile _plannerAgent;
    private readonly AgentProfile _backendAgent;
    private readonly AgentProfile _frontendAgent;
    private readonly AgentProfile _reviewerAgent;
    private readonly AgentProfile _projectManagerAgent;
    private readonly TaskLedger _ledger;

    public DirectorOrchestrator(Kernel kernel, TaskLedger ledger)
    {
        _ = kernel;
        _ledger = ledger;

        _researcherAgent = new AgentProfile(
            "Ugo_Researcher",
            "Act as a senior research engineer. Gather context, compare approaches, and summarize trade-offs.");

        _plannerAgent = new AgentProfile(
            "Ugo_Planner",
            "Break high-level goals into concrete, ordered tasks suitable for parallel execution.");

        _backendAgent = new AgentProfile(
            "Ugo_Backend",
            "Focus on C# APIs and Database logic. Use MCP tools to write files.");

        _frontendAgent = new AgentProfile(
            "Ugo_UI",
            "Focus on Blazor/React components and UX. Use MCP tools for CSS/HTML and wireframes.");

        _reviewerAgent = new AgentProfile(
            "Ugo_Reviewer",
            "Act as QA/Reviewer. Inspect diffs, reason about edge cases, and decide if work meets the goal.");

        _projectManagerAgent = new AgentProfile(
            "Ugo_PM",
            "Track project progress, update the task ledger, and coordinate hand-offs between roles.");
    }

    /// <summary>
    /// HUMAN-IN-THE-LOOP checkpoint to approve potentially destructive actions.
    /// </summary>
    public async Task<bool> HumanApprovalCheckpointAsync(string actionDescription)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n[AGENT UGO REQUIRES APPROVAL]: {actionDescription}");
        Console.Write("Proceed? (y/n): ");

        var input = await Task.Run(Console.ReadLine);
        Console.ResetColor();

        return string.Equals(input, "y", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Example of running a multi-role, parallel development task tracked through the shared TaskLedger.
    /// </summary>
    public async Task RunParallelDevTaskAsync(string goal)
    {
        // High-level planning and research
        var researchEntry = _ledger.AddTask(
            $"Research requirements for: {goal}",
            _researcherAgent.Name,
            AgentRole.Researcher);
        var planningEntry = _ledger.AddTask(
            $"Plan implementation phases for: {goal}",
            _plannerAgent.Name,
            AgentRole.Planner,
            TaskLifecycleState.DividingIntoSubtasks);

        _ledger.TryUpdateLifecycle(researchEntry.Id, TaskLifecycleState.InProgress);
        _ledger.TryUpdateLifecycle(planningEntry.Id, TaskLifecycleState.InProgress);

        var researchTask = ExecuteAgentTaskAsync(_researcherAgent, researchEntry.Description);
        var planningTask = ExecuteAgentTaskAsync(_plannerAgent, planningEntry.Description);

        await Task.WhenAll(researchTask, planningTask);

        _ledger.TryUpdateLifecycle(researchEntry.Id, TaskLifecycleState.Completed);
        _ledger.TryUpdateLifecycle(planningEntry.Id, TaskLifecycleState.Completed);

        // Parallel backend / frontend implementation
        var backendDescription = $"Backend focus: {goal}. Draft the UserAuth API logic.";
        var frontendDescription = $"Frontend focus: {goal}. Create the Login page UI.";

        var backendEntry = _ledger.AddTask(backendDescription, _backendAgent.Name, AgentRole.Coder, TaskLifecycleState.InProgress);
        var frontendEntry = _ledger.AddTask(frontendDescription, _frontendAgent.Name, AgentRole.UiUxDesigner, TaskLifecycleState.InProgress);

        var backendTask = ExecuteAgentTaskAsync(_backendAgent, backendDescription);
        var frontendTask = ExecuteAgentTaskAsync(_frontendAgent, frontendDescription);

        await Task.WhenAll(backendTask, frontendTask);

        _ledger.TryUpdateLifecycle(backendEntry.Id, TaskLifecycleState.WaitingForTests);
        _ledger.TryUpdateLifecycle(frontendEntry.Id, TaskLifecycleState.WaitingForTests);

        // Review / QA stage
        var reviewEntry = _ledger.AddTask(
            $"Review backend and frontend work for: {goal}",
            _reviewerAgent.Name,
            AgentRole.Reviewer,
            TaskLifecycleState.InProgress);

        await ExecuteAgentTaskAsync(_reviewerAgent, reviewEntry.Description);
        _ledger.TryUpdateLifecycle(reviewEntry.Id, TaskLifecycleState.WaitingForHumanFinalApproval);

        // Checkpoint before taking any destructive action (e.g., merging branches, running scripts).
        if (await HumanApprovalCheckpointAsync("Merge feature branch into main, commit to GitHub, and deploy"))
        {
            _ledger.TryUpdateLifecycle(reviewEntry.Id, TaskLifecycleState.ReadyForCommit);

            var gitEntry = _ledger.AddTask(
                $"Commit and push changes for: {goal}",
                _projectManagerAgent.Name,
                AgentRole.GitOperator,
                TaskLifecycleState.CommittingToGitHub);

            var deployEntry = _ledger.AddTask(
                $"Deploy updated services for: {goal}",
                _projectManagerAgent.Name,
                AgentRole.Deployer,
                TaskLifecycleState.Deploying);

            // TODO: Proceed with MCP Tool calls for git and deployment here.
            _ledger.TryUpdateLifecycle(gitEntry.Id, TaskLifecycleState.Completed);
            _ledger.TryUpdateLifecycle(deployEntry.Id, TaskLifecycleState.Completed);
        }
    }

    private static Task ExecuteAgentTaskAsync(AgentProfile agent, string taskDescription)
    {
        _ = agent;
        _ = taskDescription;
        return Task.Delay(TimeSpan.FromMilliseconds(200));
    }

    private sealed record AgentProfile(string Name, string Instructions);
}
