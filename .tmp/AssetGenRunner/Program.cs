using McpTestServer;

Environment.SetEnvironmentVariable("AGENT_UGO_ASSET_OUTPUT_DIR", Path.GetFullPath("..\\..\\Ugo.Orchestrator\\wwwroot"));

var faviconPrompt = "A minimalist futuristic favicon mark for Agent Ugo, neon cyan geometric owl-circuit symbol on transparent background.";
var avatarPrompt = "A futuristic friendly robotic owl avatar for Agent Ugo, neon cyan accents, engineering AI assistant style, transparent background.";

Console.WriteLine(await MediaTools.GenerateVisualAsset(faviconPrompt, "favicon"));
Console.WriteLine(await MediaTools.GenerateVisualAsset(avatarPrompt, "ugo-bot-avatar"));
