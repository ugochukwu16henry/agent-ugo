using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.SemanticKernel;
using OpenTelemetry.Trace;
using Ugo.Orchestrator;
using Ugo.Orchestrator.Core;
using Ugo.Orchestrator.Components;
using Ugo.Orchestrator.Data;
using Ugo.Orchestrator.Hubs;
using Ugo.Orchestrator.Memory;
using Ugo.Orchestrator.Services;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
var stateDirectory = Path.Combine(AppContext.BaseDirectory, "state");
Directory.CreateDirectory(stateDirectory);
var stateDbPath = Path.Combine(stateDirectory, "ugo-state.db");
var azureMonitorConnectionString =
    builder.Configuration["AzureMonitor:ConnectionString"] ??
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();
builder.Services.AddFluentUIComponents();
builder.Services.AddDbContextFactory<UgoDbContext>(options => options.UseSqlite($"Data Source={stateDbPath}"));
builder.Services.AddScoped<DurableCheckpointStore>();
builder.Services.AddSingleton<TaskLedger>();
builder.Services.AddSingleton<Kernel>(_ => Kernel.CreateBuilder().Build());
builder.Services.AddScoped<PreviewService>();
builder.Services.AddSingleton<ThoughtCacheService>();
builder.Services.AddSingleton<IMcpToolClient, NoOpMcpToolClient>();
builder.Services.AddSingleton<FastExecutionEngine>();
builder.Services.AddSingleton<TimeTravelService>();
builder.Services.AddSingleton<TelemetryProvider>();
builder.Services.AddSingleton<OrchestrationService>();
builder.Services.AddSingleton<DirectorOrchestrator>();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(TelemetryProvider.ActivitySourceName);

        if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
        {
            tracing.AddAzureMonitorTraceExporter(options => options.ConnectionString = azureMonitorConnectionString);
        }
    });

var app = builder.Build();

await using (var dbContext = await app.Services.GetRequiredService<IDbContextFactory<UgoDbContext>>().CreateDbContextAsync())
{
    await dbContext.Database.EnsureCreatedAsync();
}

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
