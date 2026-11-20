using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.Protocol;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Cli.Mcp.Tools
{
    public class PingTool : IMcpTool
    {
        public string Name => "ping";
        public string Description => "Test connection to active Revit instance.";
        public object InputSchema => new
        {
            type = "object",
            properties = new { }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            var response = await client.SendRequestAsync("ping", null, cancellationToken).ConfigureAwait(false);
            string text = response.Result != null ? JsonSerializer.Serialize(response.Result) : "pong";
            return context.FormatMcpTextResult(idValue, text);
        }
    }

    public class ListRevitInstancesTool : IMcpTool
    {
        public string Name => "list_revit_instances";
        public string Description => "List all running Revit instances with version, PID, active document, and UI status.";
        public object InputSchema => new
        {
            type = "object",
            properties = new { }
        };

        public Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var sessions = context.Router.GetActiveSessions();
            string json = JsonSerializer.Serialize(sessions, context.JsonOptions);
            return Task.FromResult(context.FormatMcpTextResult(idValue, json));
        }
    }

    public class SelectRevitInstanceTool : IMcpTool
    {
        public string Name => "select_revit_instance";
        public string Description => "Select target Revit instance for subsequent operations when multiple instances are running.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                processId = new
                {
                    type = "integer",
                    description = "Process ID (PID) of the Revit instance to connect to."
                }
            },
            required = new[] { "processId" }
        };

        public Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            int pid = arguments?["processId"]?.GetValue<int>() ?? 0;
            if (pid <= 0)
            {
                return Task.FromResult(context.FormatMcpErrorResult(idValue, "Valid processId is required."));
            }

            context.Router.SelectInstance(pid);
            return Task.FromResult(context.FormatMcpTextResult(idValue, $"Selected Revit instance with PID {pid}."));
        }
    }

    public class ExecuteCSharpTool : IMcpTool
    {
        public string Name => "execute_csharp";
        public string Description => "Execute arbitrary C# code directly inside Revit with full API access (UIApplication, Document, UIDocument), automatic transaction management, and live activity tracking in the Revit UI.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                code = new
                {
                    type = "string",
                    description = "C# code snippet or class. Available: 'IRevitScriptContext context' (Doc, App, UIDoc, WithTransaction, WithTransactionGroup, Print, AddAffectedElements)."
                },
                actionSummary = new
                {
                    type = "string",
                    description = "Brief human-readable summary of the action in the user's language (e.g. 'Creating levels and grids'). Displayed live in the Revit DockablePane."
                },
                agentName = new
                {
                    type = "string",
                    description = "Name of the agent executing the action (e.g. 'Claude', 'Antigravity')."
                },
                transactionName = new
                {
                    type = "string",
                    description = "Optional name for an enclosing Revit Transaction (appears in Revit Undo history). Omit if script manages its own transactions or performs read-only operations."
                },
                arguments = new
                {
                    type = "object",
                    description = "Optional dictionary of arguments passed to the script as 'args'."
                }
            },
            required = new[] { "code" }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);

            string? code = arguments?["code"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(code))
            {
                return context.FormatMcpErrorResult(idValue, "Code cannot be empty.");
            }

            var request = new ExecuteScriptRequest
            {
                Code = code,
                ActionSummary = arguments?["actionSummary"]?.GetValue<string>(),
                AgentName = arguments?["agentName"]?.GetValue<string>(),
                TransactionName = arguments?["transactionName"]?.GetValue<string>()
            };

            if (arguments?["arguments"] is JsonObject argObj)
            {
                request.Arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argObj.ToJsonString());
            }

            var result = await client.ExecuteCSharpAsync(request, cancellationToken).ConfigureAwait(false);
            string resultJson = JsonSerializer.Serialize(result, context.JsonOptions);

            if (!result.Success)
            {
                return context.FormatMcpErrorResult(idValue, $"Script failed ({result.ErrorCode}): {result.ErrorMessage}\nOutput:\n{result.ConsoleOutput}");
            }

            return context.FormatMcpTextResult(idValue, resultJson);
        }
    }
}
