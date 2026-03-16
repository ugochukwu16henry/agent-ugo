using Microsoft.FluentUI.AspNetCore.Components;
using Ugo.Orchestrator.Components;
using Ugo.Orchestrator.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();
builder.Services.AddFluentUIComponents();

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
