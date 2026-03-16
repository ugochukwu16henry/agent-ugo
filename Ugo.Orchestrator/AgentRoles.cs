namespace Ugo.Orchestrator;

/// <summary>
/// High-level functional roles that Agent Ugo can assume.
/// These map to specialized agents or behaviors in the orchestration graph.
/// </summary>
public enum AgentRole
{
    Researcher,
    Planner,
    Coder,
    Reviewer,
    UiUxDesigner,
    ProjectManager,
    WireframeDesigner,
    ProgressTracker,
    TaskDivider,
    Deployer,
    GitOperator,
    Persistence
}

/// <summary>
/// Fine-grained lifecycle states for tasks in the ledger.
/// </summary>
public enum TaskLifecycleState
{
    New,
    DividingIntoSubtasks,
    InProgress,
    WaitingForTests,
    WaitingForHumanFinalApproval,
    ReadyForCommit,
    CommittingToGitHub,
    Deploying,
    Completed,
    Failed
}

