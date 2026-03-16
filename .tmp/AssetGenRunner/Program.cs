using McpTestServer;

Environment.SetEnvironmentVariable("AGENT_UGO_ASSET_OUTPUT_DIR", Path.GetFullPath("..\\..\\Ugo.Orchestrator\\wwwroot"));

var prompt = "A minimalist, futuristic geometric logo of an owl head merged with a circuit board, neon cyan on a transparent background, high-tech engineering style.";
var result = await MediaTools.GenerateVisualAsset(prompt, "logo");
Console.WriteLine(result);
