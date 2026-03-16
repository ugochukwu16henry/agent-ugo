using System;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

namespace Ugo.Orchestrator;

public sealed class DirectorOrchestrator
{
    private readonly ChatCompletionAgent _researcherAgent;
    private readonly ChatCompletionAgent _plannerAgent;
    private readonly ChatCompletionAgent _backendAgent;
    private readonly ChatCompletionAgent _frontendAgent;
    private readonly ChatCompletionAgent _reviewerAgent;
    private readonly ChatCompletionAgent _projectManagerAgent;
    private readonly AgentGroupChat _director;
    private readonly TaskLedger _ledger;

    public DirectorOrchestrator(Kernel kernel, TaskLedger ledger)
    {
        _ledger = ledger;

        _researcherAgent = new ChatCompletionAgent(kernel)
        {
            Name = "Ugo_Researcher",
            Instructions = "Act as a senior research engineer. Gather context, compare approaches, and summarize trade-offs."
        };

        _plannerAgent = new ChatCompletionAgent(kernel)
        {
            Name = "Ugo_Planner",
            Instructions = "Break high-level goals into concrete, ordered tasks suitable for parallel execution."
        };

        _backendAgent = new ChatCompletionAgent(kernel)
        {
            Name = "Ugo_Backend",
            Instructions = "Focus on C# APIs and Database logic. Use MCP tools to write files."
        };

        _frontendAgent = new ChatCompletionAgent(kernel)
        {
            Name = "Ugo_UI",
            Instructions = "Focus on Blazor/React components and UX. Use MCP tools for CSS/HTML and wireframes."
        };

        _reviewerAgent = new ChatCompletionAgent(kernel)
        {
            Name = "Ugo_Reviewer",
            Instructions = "Act as QA/Reviewer. Inspect diffs, reason about edge cases, and decide if work meets the goal."
        };

        _projectManagerAgent = new ChatCompletionAgent(kernel)
        {
            Name = "Ugo_PM",
            Instructions = "Track project progress, update the task ledger, and coordinate hand-offs between roles."
        };

        _director = new AgentGroupChat(kernel,
            _researcherAgent,
            _plannerAgent,
            _backendAgent,
            _frontendAgent,
            _reviewerAgent,
            _projectManagerAgent)
        {
            ExecutionSettings = new()
            {
                SelectionStrategy = new KernelFunctionSelectionStrategy()
            }
        };
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

        var researchTask = _director.InvokeAsync(researchEntry.Description);
        var planningTask = _director.InvokeAsync(planningEntry.Description);

        await Task.WhenAll(researchTask.ToTask(), planningTask.ToTask());

        _ledger.TryUpdateLifecycle(researchEntry.Id, TaskLifecycleState.Completed);
        _ledger.TryUpdateLifecycle(planningEntry.Id, TaskLifecycleState.Completed);

        // Parallel backend / frontend implementation
        var backendDescription = $"Backend focus: {goal}. Draft the UserAuth API logic.";
        var frontendDescription = $"Frontend focus: {goal}. Create the Login page UI.";

        var backendEntry = _ledger.AddTask(backendDescription, _backendAgent.Name, AgentRole.Coder, TaskLifecycleState.InProgress);
        var frontendEntry = _ledger.AddTask(frontendDescription, _frontendAgent.Name, AgentRole.UiUxDesigner, TaskLifecycleState.InProgress);

        var backendTask = _director.InvokeAsync(backendDescription);
        var frontendTask = _director.InvokeAsync(frontendDescription);

        await Task.WhenAll(backendTask.ToTask(), frontendTask.ToTask());

        _ledger.TryUpdateLifecycle(backendEntry.Id, TaskLifecycleState.WaitingForTests);
        _ledger.TryUpdateLifecycle(frontendEntry.Id, TaskLifecycleState.WaitingForTests);

        // Review / QA stage
        var reviewEntry = _ledger.AddTask(
            $"Review backend and frontend work for: {goal}",
            _reviewerAgent.Name,
            AgentRole.Reviewer,
            TaskLifecycleState.InProgress);

        var reviewTask = _director.InvokeAsync(reviewEntry.Description);
        await reviewTask.ToTask();
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
}
