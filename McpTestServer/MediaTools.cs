using System.ComponentModel;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Playwright;
using ModelContextProtocol.Server;
using QuestPDF.Fluent;
using Ugo.Orchestrator.Hubs;

namespace McpTestServer;

[McpServerToolType]
public static class MediaTools
{
    [McpServerTool, Description("Generates professional logos, wireframes, or color palettes.")]
    public static async Task<string> GenerateVisualAsset(string description, string assetType)
    {
        _ = description;

        var normalizedType = string.IsNullOrWhiteSpace(assetType) ? "asset" : assetType.Trim().ToLowerInvariant();
        var fileName = normalizedType switch
        {
            "logo" => "logo.png",
            "favicon" => "favicon.ico",
            "ugo-bot-avatar" => "ugo-bot-avatar.png",
            _ => $"{normalizedType}_{Guid.NewGuid():N}.png"
        };

        var configuredOutput = Environment.GetEnvironmentVariable("AGENT_UGO_ASSET_OUTPUT_DIR");
        var assetDirectory = string.IsNullOrWhiteSpace(configuredOutput)
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot", "assets")
            : configuredOutput;

        Directory.CreateDirectory(assetDirectory);

        var fullPath = Path.Combine(assetDirectory, fileName);

        var imageBytes = normalizedType == "favicon"
            ? CreatePlaceholderIco()
            : CreatePlaceholderPng();

        await File.WriteAllBytesAsync(fullPath, imageBytes);

        return $"Asset generated successfully: {fullPath}";
    }

    private static byte[] CreatePlaceholderPng()
    {
        const string placeholderPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==";
        return Convert.FromBase64String(placeholderPngBase64);
    }

    private static byte[] CreatePlaceholderIco()
    {
        return
        [
            0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
            0x01, 0x01, 0x00, 0x00,
            0x01, 0x00, 0x20, 0x00,
            0x30, 0x00, 0x00, 0x00,
            0x16, 0x00, 0x00, 0x00,
            0x28, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x20, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xFF, 0xFF, 0x00, 0xFF,
            0x00, 0x00, 0x00, 0x00
        ];
    }

    [McpServerTool, Description("Creates a PDF document based on project data.")]
    public static string CreateProjectDoc(string content, string format)
    {
        if (!string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "Only 'pdf' is supported in this build.";
        }

        var outputPath = Path.Combine(AppContext.BaseDirectory, "ProjectReport.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Content().Text(content);
            });
        }).GeneratePdf(outputPath);

        return $"Document created at: {outputPath}";
    }

    [McpServerTool, Description("Captures a headless browser screenshot and streams the base64 preview to the Dashboard via SignalR.")]
    public static async Task<string> CapturePreview(
        string url,
        int width = 1440,
        int height = 900,
        int waitMs = 1200)
    {
        var targetUrl = string.IsNullOrWhiteSpace(url) ? "http://localhost:5001" : url;
        var hubUrl = Environment.GetEnvironmentVariable("AGENTUGO_ORCHESTRATOR_HUB_URL") ?? "http://localhost:5288/ugohub";

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = width,
                    Height = height
                }
            });

            await page.GotoAsync(targetUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 20000
            });

            if (waitMs > 0)
            {
                await page.WaitForTimeoutAsync(waitMs);
            }

            var png = await page.ScreenshotAsync(new PageScreenshotOptions
            {
                FullPage = true,
                Type = ScreenshotType.Png
            });

            var base64 = Convert.ToBase64String(png);

            await using var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync();

            await connection.InvokeAsync("BroadcastInternalTrace", new InternalTraceMessage(
                Kind: "ToolCall",
                Source: "CapturePreview",
                Content: $"Captured preview from {targetUrl}",
                Status: "Success",
                Timestamp: DateTimeOffset.UtcNow));

            await connection.InvokeAsync("BroadcastPreviewFrame", new PreviewFrameMessage(
                Base64Png: base64,
                SourceUrl: targetUrl,
                CapturedAt: DateTimeOffset.UtcNow,
                Note: "Headless Playwright capture."));

            return $"Preview captured and sent to dashboard from {targetUrl}.";
        }
        catch (PlaywrightException ex)
        {
            return $"CapturePreview failed: {ex.Message}. If browser binaries are missing, run 'playwright install chromium'.";
        }
        catch (Exception ex)
        {
            return $"CapturePreview failed: {ex.Message}";
        }
    }
}

