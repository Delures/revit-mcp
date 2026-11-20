using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Cli.Mcp.Tools;
using RevitMCP.Cli.Routing;
using RevitMCP.Shared.Knowledge;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Cli.Mcp
{
    public class McpServer
    {
        private readonly RevitSessionRouter _router = new();
        private readonly AgentKnowledgeManager _knowledgeManager = new();
        private readonly McpToolRegistry _toolRegistry;
        private readonly McpToolContext _toolContext;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public McpServer()
        {
            _toolRegistry = McpToolRegistry.CreateDefault();
            _toolContext = new McpToolContext(_router, _knowledgeManager, _jsonOptions);
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var encoding = new System.Text.UTF8Encoding(false);
            using var reader = new StreamReader(Console.OpenStandardInput(), encoding);
            using var writer = new StreamWriter(Console.OpenStandardOutput(), encoding) { AutoFlush = true };

            while (!cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var jsonNode = JsonNode.Parse(line);
                    if (jsonNode == null) continue;

                    string? method = jsonNode["method"]?.GetValue<string>();
                    var id = jsonNode["id"];

                    // Notifications have no id
                    if (id == null && method?.StartsWith("notifications/") == true)
                    {
                        continue;
                    }

                    var response = await HandleMessageAsync(method, jsonNode["params"], id, cancellationToken).ConfigureAwait(false);
                    if (response != null)
                    {
                        string respJson = JsonSerializer.Serialize(response, _jsonOptions);
                        await writer.WriteLineAsync(respJson.AsMemory(), cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    var errResponse = JsonRpcResponse.CreateError(null, JsonRpcErrorCodes.InternalError, ex.Message);
                    string respJson = JsonSerializer.Serialize(errResponse, _jsonOptions);
                    await writer.WriteLineAsync(respJson.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task<JsonRpcResponse?> HandleMessageAsync(string? method, JsonNode? parameters, JsonNode? idNode, CancellationToken cancellationToken)
        {
            object? idValue = idNode?.AsValue().GetValue<object>();

            switch (method)
            {
                case "initialize":
                    return JsonRpcResponse.Success(idValue, new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { },
                            resources = new { }
                        },
                        serverInfo = new
                        {
                            name = "revit-mcp",
                            version = VersionInfo.Current
                        }
                    });

                case "ping":
                    return JsonRpcResponse.Success(idValue, new { });

                case "tools/list":
                    return JsonRpcResponse.Success(idValue, new
                    {
                        tools = _toolRegistry.GetToolsListSchema()
                    });

                case "tools/call":
                    return await HandleToolCallAsync(idValue, parameters, cancellationToken).ConfigureAwait(false);

                case "resources/list":
                    return JsonRpcResponse.Success(idValue, new
                    {
                        resources = new[]
                        {
                            new
                            {
                                uri = "revit://sessions",
                                name = "Active Revit Sessions",
                                mimeType = "application/json",
                                description = "Information about running Revit instances with Revit MCP plugin loaded"
                            },
                            new
                            {
                                uri = "revit://knowledge/rules",
                                name = "Revit MCP Knowledge Base & Rules",
                                mimeType = "text/markdown",
                                description = "Aggregated lessons learned, API quirks, and best practices for developing Revit API scripts"
                            }
                        }
                    });

                case "resources/read":
                    return HandleResourceRead(idValue, parameters);

                default:
                    return JsonRpcResponse.CreateError(idValue, JsonRpcErrorCodes.MethodNotFound, $"Method '{method}' not found.");
            }
        }

        private async Task<JsonRpcResponse> HandleToolCallAsync(object? idValue, JsonNode? parameters, CancellationToken cancellationToken)
        {
            string? toolName = parameters?["name"]?.GetValue<string>();
            var args = parameters?["arguments"];

            if (string.IsNullOrEmpty(toolName))
            {
                return _toolContext.FormatMcpErrorResult(idValue, "Missing tool name.");
            }

            if (!_toolRegistry.TryGetTool(toolName, out var tool) || tool == null)
            {
                return _toolContext.FormatMcpErrorResult(idValue, $"Unknown tool '{toolName}'.");
            }

            try
            {
                return await tool.ExecuteAsync(_toolContext, idValue, args, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return _toolContext.FormatMcpErrorResult(idValue, $"Tool '{toolName}' execution failed: {ex.Message}");
            }
        }

        private JsonRpcResponse HandleResourceRead(object? idValue, JsonNode? parameters)
        {
            string? uri = parameters?["uri"]?.GetValue<string>();
            if (uri == "revit://sessions")
            {
                var sessions = _router.GetActiveSessions();
                string json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true });
                return JsonRpcResponse.Success(idValue, new
                {
                    contents = new[]
                    {
                        new
                        {
                            uri = "revit://sessions",
                            mimeType = "application/json",
                            text = json
                        }
                    }
                });
            }

            if (uri == "revit://knowledge/rules")
            {
                string markdown = _knowledgeManager.GetAggregatedRules();
                return JsonRpcResponse.Success(idValue, new
                {
                    contents = new[]
                    {
                        new
                        {
                            uri = "revit://knowledge/rules",
                            mimeType = "text/markdown",
                            text = markdown
                        }
                    }
                });
            }

            return JsonRpcResponse.CreateError(idValue, JsonRpcErrorCodes.InvalidParams, $"Resource '{uri}' not found.");
        }
    }
}
