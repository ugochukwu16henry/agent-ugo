using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ugo.Orchestrator.Core;

/// <summary>
/// Encapsulates the self-correction loop for Agent Ugo.
/// A selector delegate is used so that:
/// - If the tester reports "Test Failures", the coder is re-invoked with the error log
/// - If the coder reports "Code Saved", the tester is re-invoked
/// - If "All tests passed" appears, control returns to the Director
/// with a guard of maxRetries to avoid infinite oscillation.
/// </summary>
public static class SelfCorrectionLoop
{
    public static Func<string, string> CreateSelectionStrategy()
        => lastMessage =>
        {
            if (lastMessage.Contains("Test Failures", StringComparison.OrdinalIgnoreCase))
            {
                return "Coder";
            }

            if (lastMessage.Contains("Code Saved", StringComparison.OrdinalIgnoreCase))
            {
                return "Tester";
            }

            if (lastMessage.Contains("All tests passed", StringComparison.OrdinalIgnoreCase))
            {
                return "Director";
            }

            return "Coder";
        };

    /// <summary>
    /// Runs a bounded self-correction loop between the coder and tester roles.
    /// </summary>
    public static async Task RunAsync(
        Func<string, Task<string>> coderStep,
        Func<string, Task<string>> testerStep,
        Func<string, Task> directorStep,
        int maxRetries = 5,
        CancellationToken cancellationToken = default)
    {
        if (coderStep is null) throw new ArgumentNullException(nameof(coderStep));
        if (testerStep is null) throw new ArgumentNullException(nameof(testerStep));
        if (directorStep is null) throw new ArgumentNullException(nameof(directorStep));

        var lastMessage = string.Empty;
        var retries = 0;
        var selectNextRole = CreateSelectionStrategy();

        while (retries < maxRetries && !cancellationToken.IsCancellationRequested)
        {
            var nextRole = selectNextRole(lastMessage);

            switch (nextRole)
            {
                case "Coder":
                    lastMessage = await coderStep(lastMessage);
                    retries++;
                    break;
                case "Tester":
                    lastMessage = await testerStep(lastMessage);
                    retries++;
                    break;
                case "Director":
                    await directorStep(lastMessage);
                    return;
            }
        }
    }
}

