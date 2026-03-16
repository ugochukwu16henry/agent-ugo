namespace Ugo.Orchestrator.Services;

public sealed class PreviewService
{
    public string? CurrentPreviewBase64 { get; private set; }

    public void UpdatePreview(string? base64Png)
    {
        CurrentPreviewBase64 = base64Png;
    }
}