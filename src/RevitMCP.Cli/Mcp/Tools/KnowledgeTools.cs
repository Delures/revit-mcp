using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.Knowledge;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Cli.Mcp.Tools
{
    public class RecordLessonTool : IMcpTool
    {
        public string Name => "record_lesson";
        public string Description => "Record an API insight, pitfall, workaround or lesson learned to long-term memory markdown store (%LocalAppData%/RevitMCP/knowledge).";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                topic = new
                {
                    type = "string",
                    description = "Concise topic title (e.g. 'Wall Creation Coordinates')."
                },
                content = new
                {
                    type = "string",
                    description = "Markdown content detailing problem and solution."
                },
                category = new
                {
                    type = "string",
                    description = "Category (e.g. 'Geometry', 'Parameters', 'Transactions', 'MEP', 'General')."
                },
                revitVersions = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "Applicable Revit versions (e.g. ['2024', '2025'])."
                }
            },
            required = new[] { "topic", "content" }
        };

        public Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<RecordLessonRequest>(json, context.JsonOptions);
            if (req == null || string.IsNullOrWhiteSpace(req.Topic) || string.IsNullOrWhiteSpace(req.Content))
            {
                return Task.FromResult(context.FormatMcpErrorResult(idValue, "Both 'topic' and 'content' are required for record_lesson."));
            }

            var lesson = context.KnowledgeManager.RecordLesson(req);
            return Task.FromResult(context.FormatMcpTextResult(idValue, $"Lesson recorded successfully:\nTopic: {lesson.Topic}\nCategory: {lesson.Category}\nFile: {lesson.FilePath}"));
        }
    }

    public class QueryKnowledgeTool : IMcpTool
    {
        public string Name => "query_knowledge";
        public string Description => "Search agent knowledge base and lessons learned on Revit API quirks, coordinate conversions, and C# patterns.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description = "Search query keyword or phrase."
                },
                category = new
                {
                    type = "string",
                    description = "Optional category filter."
                }
            },
            required = new[] { "query" }
        };

        public Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<QueryKnowledgeRequest>(json, context.JsonOptions);
            string query = req?.Query ?? string.Empty;
            string? cat = req?.Category;
            var lessons = context.KnowledgeManager.Query(query, cat);
            return Task.FromResult(context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(lessons, context.JsonOptions)));
        }
    }
}
