using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Cli.Mcp.Tools
{
    public class ManageParametersTool : IMcpTool
    {
        public string Name => "manage_parameters";
        public string Description => "Batch read or write any parameters on elements by name, BuiltInParameter, or GUID, with automatic metric/SI unit conversion (e.g. mm, m, m2, degrees).";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of parameter modification in user's language for the Revit activity pane (e.g. 'Setting concrete cover parameters')."
                },
                transactionName = new
                {
                    type = "string",
                    description = "Short human-readable transaction name in user's language for Revit Undo (e.g. 'Set Parameters', 'Parameter ändern')."
                },
                action = new
                {
                    type = "string",
                    description = "'get' to read parameters, 'set' to modify parameters (default 'get').",
                    @enum = new[] { "get", "set" }
                },
                elementIds = new
                {
                    type = "array",
                    items = new { type = "integer" },
                    description = "Array of element IDs."
                },
                parameters = new
                {
                    type = "array",
                    description = "Array of parameter specs: { name?: string, builtInName?: string, guid?: string, value?: any, unit?: string }.",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Parameter name (e.g. 'Mark', 'Comments', 'Base Level')." },
                            builtInName = new { type = "string", description = "BuiltInParameter name (e.g. 'ALL_MODEL_MARK', 'WALL_USER_HEIGHT_PARAM')." },
                            guid = new { type = "string", description = "Shared parameter GUID." },
                            value = new { description = "Value to set (string, number, boolean, elementId)." },
                            unit = new { type = "string", description = "User unit for automatic conversion (e.g. 'mm', 'm', 'm2', 'm3', 'degrees')." }
                        }
                    }
                }
            },
            required = new[] { "elementIds" }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<ManageParametersRequest>(json, context.JsonOptions) ?? new ManageParametersRequest();
            var result = await client.ManageParametersAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class ManageDocumentsTool : IMcpTool
    {
        public string Name => "manage_documents";
        public string Description => "Manage Revit documents: list open documents, open .rvt/.rfa files, save, save_as, close, or synchronize with central.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of document operation in user's language for the Revit activity pane (e.g. 'Opening project model')."
                },
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'list', 'open', 'save', 'save_as', 'close', 'sync_with_central'.",
                    @enum = new[] { "list", "open", "save", "save_as", "close", "sync_with_central" }
                },
                filePath = new
                {
                    type = "string",
                    description = "File path for 'open' or 'save_as'."
                },
                save = new
                {
                    type = "boolean",
                    description = "Whether to save changes before closing (for 'close'). Default false."
                },
                comment = new
                {
                    type = "string",
                    description = "Sync comment for 'sync_with_central'."
                },
                showUi = new
                {
                    type = "boolean",
                    description = "Whether to open document in Revit UI (default true)."
                },
                detachMode = new
                {
                    type = "string",
                    description = "Detach-from-central option for 'open': 'none' (default), 'preserve_worksets' (DetachAndPreserveWorksets), 'discard_worksets' (DetachAndDiscardWorksets).",
                    @enum = new[] { "none", "preserve_worksets", "discard_worksets" }
                },
                worksets = new
                {
                    description = "Worksets to open for 'open': 'all' (default), 'none', 'editable', or an array of workset names/prefixes."
                },
                audit = new
                {
                    type = "boolean",
                    description = "Whether to run an audit when opening (default false)."
                },
                suppressDialogs = new
                {
                    type = "boolean",
                    description = "Whether to suppress open dialogs/warnings (default true when detachMode != 'none')."
                }
            },
            required = new[] { "action" }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<ManageDocumentsRequest>(json, context.JsonOptions) ?? new ManageDocumentsRequest();
            var result = await client.ManageDocumentsAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class ExportScheduleTool : IMcpTool
    {
        public string Name => "export_schedule";
        public string Description => "Read Revit schedule tables (ViewSchedule) into structured rows and columns (e.g. steel takeoffs, room finishes, door/window schedules, material takeoffs). Action 'list' returns available schedules, action 'get_data' returns table headers and rows.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of schedule export in user's language for the Revit activity pane (e.g. 'Exporting rebar schedule')."
                },
                action = new
                {
                    type = "string",
                    description = "'list' to view all schedules in the model, or 'get_data' to extract table rows (default 'get_data').",
                    @enum = new[] { "list", "get_data" }
                },
                scheduleName = new
                {
                    type = "string",
                    description = "Name of the schedule to read (fuzzy match supported, e.g. 'Steel Takeoff', 'Door Schedule')."
                },
                scheduleId = new
                {
                    type = "integer",
                    description = "Optional explicit ViewSchedule element ID."
                },
                maxRows = new
                {
                    type = "integer",
                    description = "Maximum number of rows to return (default 500, max 5000)."
                }
            }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<ExportScheduleRequest>(json, context.JsonOptions) ?? new ExportScheduleRequest();
            var result = await client.ExportScheduleAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }
}
