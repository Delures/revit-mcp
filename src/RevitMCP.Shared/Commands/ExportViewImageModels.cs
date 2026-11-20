using System.Collections.Generic;

namespace RevitMCP.Shared.Commands
{
    public class ExportViewImageRequest : IAgentAction
    {
        public long? ViewId { get; set; }
        public string? ViewName { get; set; }
        public string Format { get; set; } = "png"; // "png" or "jpeg"
        public int PixelSize { get; set; } = 1024;
        public bool ReturnBase64 { get; set; } = true;
        public string? OutputFilePath { get; set; }
        public string? ActionSummary { get; set; }
        public string? AgentName { get; set; }
    }

    public class ExportViewImageResult
    {
        public bool Success { get; set; } = true;
        public long ViewId { get; set; }
        public string ViewName { get; set; } = string.Empty;
        public string Format { get; set; } = "png";
        public string MimeType { get; set; } = "image/png";
        public int Width { get; set; }
        public int Height { get; set; }
        public long FileSizeBytes { get; set; }
        public string? SavedPath { get; set; }
        public string? Base64Data { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
