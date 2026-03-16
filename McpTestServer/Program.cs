using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();

/// <summary>
/// TestTools provides Agent Ugo with the ability to validate code changes.
/// This verification loop supports autonomous self-correction by executing tests locally.
/// </summary>
[McpServerToolType]
public static class TestTools
{
    /// <summary>
    /// Runs <c>dotnet test</c> for the specified project path in a local subprocess and returns combined diagnostics.
    /// </summary>
    /// <param name="projectPath">The full path to a test project or solution to execute.</param>
    /// <returns>
    /// A formatted string containing test output and errors. If the process cannot be started,
    /// returns a descriptive startup failure message.
    /// </returns>
    /// <remarks>
    /// This method intentionally bypasses IDE-integrated test execution limitations by invoking
    /// the local .NET CLI in a subprocess. Running in-process with the local runtime ensures the
    /// agent can access the same environment, SDK resolution, and test behavior as a human developer.
    /// </remarks>
    [McpServerTool, Description("Runs dotnet test on the specified project path and returns results.")]
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

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return string.IsNullOrWhiteSpace(error)
                ? $"--- TEST RESULTS ---\n{output}"
                : $"--- TEST FAILURES ---\n{error}\n\n--- OUTPUT ---\n{output}";
        }
        catch (Exception ex)
        {
            return $"Critical Exception while running tests: {ex.Message}";
        }
    }
}
