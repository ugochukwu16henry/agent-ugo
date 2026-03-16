using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;
using Ugo.Orchestrator;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Register the orchestration brain for Agent Ugo.
builder.Services.AddSingleton<TaskLedger>();
builder.Services.AddSingleton<Kernel>(_ =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    // TODO: Configure real AI service(s) here (OpenAI, Azure OpenAI, etc.).
    return kernelBuilder.Build();
});
builder.Services.AddSingleton<DirectorOrchestrator>();

var app = builder.Build();

// Example: kick off a parallel development task coordinated by the Director.
using (var scope = app.Services.CreateScope())
{
    var director = scope.ServiceProvider.GetRequiredService<DirectorOrchestrator>();
    await director.RunParallelDevTaskAsync("Initial multi-agent dev bootstrap");
}

await app.RunAsync();
