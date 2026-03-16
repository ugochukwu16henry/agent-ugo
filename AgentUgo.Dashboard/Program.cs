using AgentUgo.Dashboard.Components;
using AgentUgo.Dashboard.Hubs;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/octet-stream"]);
});

var app = builder.Build();

// Ensure a logo asset exists in wwwroot at startup.
var wwwroot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwroot);
var logoPath = Path.Combine(wwwroot, "logo.png");
if (!File.Exists(logoPath))
{
    const string placeholderPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAABJ0lEQVR4nO3XwQ2DMBRF0XcI7EFaKBm1AJsIk2QDrLBJoB1aQLcQzTAQnRCUXVEQrhUMf//9sfev3p7u7+1fX5d8nq+vR6PRaDQaDQaDQaDQaDQaD4cP5T+cK8Lx/GH8QfhB/EH4QfxB+EH8QfhB/EH4QfxB+EH8QfhB/EH4QfxB+EH8QfhB/EH4QfxB+EH8QfhB/EH4QfxB+EH8w3iuvt9vt9vt9vt9vt9vt9vt9vt9vt9v/1e7/X7/X7/X7/X7/X7/X7/X7/X7/X7/Xb7fb7fb7fb7fb7fb7fb7fb7fb7fb7/Yf1fLf4QfhB/EH4QfxB+EH8QfhB/EH4QfxB+EH8QfhB/EH4QfxB+EH8QfhB/EH4QfxB+EH8QfhB/EH4QfxB+EH8w/lP58vxh/ED8YfxA/GP8QPxh/ED8YfxA/GP8QPxh/ED8YfxA/GP8QPxh/ED8YfxA/GN8BN9f4J3WbIiEAAAAASUVORK5CYII=";
    var bytes = Convert.FromBase64String(placeholderPngBase64);
    await File.WriteAllBytesAsync(logoPath, bytes);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseResponseCompression();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<AgentUgoHub>("/agentUgoHub");

app.Run();
