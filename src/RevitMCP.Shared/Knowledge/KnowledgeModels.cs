using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RevitMCP.Shared.Knowledge
{
    public class KnowledgeLesson
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("topic")]
        public string Topic { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "General";

        [JsonPropertyName("revitVersions")]
        public List<string> RevitVersions { get; set; } = new List<string>();

        [JsonPropertyName("created")]
        public string Created { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("filePath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FilePath { get; set; }
    }

    public class RecordLessonRequest
    {
        [JsonPropertyName("topic")]
        public string Topic { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "General";

        [JsonPropertyName("revitVersions")]
        public List<string>? RevitVersions { get; set; }
    }

    public class QueryKnowledgeRequest
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string? Category { get; set; }
    }
}
