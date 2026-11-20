using System.Text.Json;
using RevitMCP.Cli.Routing;
using RevitMCP.Shared.Knowledge;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Cli.Mcp.Tools
{
    public class McpToolContext
    {
        public RevitSessionRouter Router { get; }
        public AgentKnowledgeManager KnowledgeManager { get; }
        public JsonSerializerOptions JsonOptions { get; }

        public McpToolContext(RevitSessionRouter router, AgentKnowledgeManager knowledgeManager, JsonSerializerOptions jsonOptions)
        {
            Router = router;
            KnowledgeManager = knowledgeManager;
            JsonOptions = jsonOptions;
        }

        public JsonRpcResponse FormatMcpTextResult(object? idValue, string text, bool isError = false)
        {
            return JsonRpcResponse.Success(idValue, new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = text
                    }
                },
                isError = isError
            });
        }

        public JsonRpcResponse FormatMcpErrorResult(object? idValue, string errorMessage)
        {
            return FormatMcpTextResult(idValue, errorMessage, isError: true);
        }
    }
}
