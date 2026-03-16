using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.SemanticKernel;
using Ugo.Orchestrator;
using Ugo.Orchestrator.Components;
using Ugo.Orchestrator.Hubs;
using Ugo.Orchestrator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();
builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton<TaskLedger>();
builder.Services.AddSingleton<Kernel>(_ => Kernel.CreateBuilder().Build());
builder.Services.AddSingleton<OrchestrationService>();
builder.Services.AddSingleton<DirectorOrchestrator>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapHub<AgentUgoHub>("/ugohub");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
