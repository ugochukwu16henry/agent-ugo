using System;
using Microsoft.Extensions.AI;

namespace Ugo.Orchestrator;

/// <summary>
/// Centralizes Agent Ugo's safety rules around tool usage.
/// Destructive or high-impact functions are classified so the
/// orchestration loop can require human approval before execution.
/// </summary>
public static class UgoSecurityPolicy
{
    private static readonly string[] CriticalKeywords =
    [
        "delete",
        "save",
        "push",
        "runtests"
    ];

    public static bool RequiresApproval(AIFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return RequiresApproval(function.Name ?? string.Empty);
    }

    public static bool RequiresApproval(string functionName)
    {
        var lower = functionName.ToLowerInvariant();

        foreach (var keyword in CriticalKeywords)
        {
            if (lower.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the function unchanged. Approval enforcement now happens
    /// in the orchestration loop once a tool has been classified as critical.
    /// </summary>
    public static AIFunction WrapWithSafety(AIFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return function;
    }
}

