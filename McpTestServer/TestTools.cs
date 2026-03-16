using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace McpTestServer;

/// <summary>
/// MCP-style helper methods exposed by Agent Ugo's test server.
/// </summary>
public static class TestTools
{
    /// <summary>
    /// Runs <c>dotnet test</c> for the specified project path in a local subprocess
    /// and returns combined diagnostics.
    /// </summary>
    public static async Task<string> RunProjectTests(string projectPath)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"test \"{projectPath}\" --logger:console;verbosity=normal",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return "Error: Could not start the dotnet test process.";
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return string.IsNullOrWhiteSpace(error)
                ? $"--- TEST RESULTS ---\n{output}"
                : $"--- TEST FAILURES ---\n{error}\n\n--- OUTPUT ---\n{output}";
        }
        catch (Exception ex)
        {
            return $"Critical exception while running tests: {ex.Message}";
        }
    }
}
