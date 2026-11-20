using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.BimMap;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Cli.Mcp.Tools
{
    public class InspectSchemaTool : IMcpTool
    {
        public string Name => "inspect_schema";
        public string Description => "Inspect project categories, family types, and parameter definitions without modifying the model.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of schema inspection in user's language for the Revit activity pane (e.g. 'Inspecting wall types')."
                },
                category = new
                {
                    type = "string",
                    description = "Category name or BuiltInCategory (e.g. 'Walls', 'OST_Walls',). If omitted, lists active model categories."
                },
                family = new
                {
                    type = "string",
                    description = "Optional family name to filter types by."
                },
                includeParameters = new
                {
                    type = "boolean",
                    description = "Whether to inspect parameter definitions on types (default false)."
                },
                sections = new
                {
                    type = "array",
                    items = new
                    {
                        type = "string",
                        @enum = new[] { "categories", "project_info", "units", "coordinates", "grids_levels", "worksets", "links_imports", "shared_params", "warnings", "rooms" }
                    },
                    description = "Optional list of schema sections to collect: 'categories' (default), 'project_info', 'units', 'coordinates', 'grids_levels', 'worksets', 'links_imports', 'shared_params', 'warnings', 'rooms'. Use 'all' for every section."
                }
            }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<InspectSchemaRequest>(json, context.JsonOptions) ?? new InspectSchemaRequest();
            var result = await client.InspectSchemaAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class QueryElementsTool : IMcpTool
    {
        public string Name => "query_elements";
        public string Description => "Query elements in active Revit document with category, level, spatial bounding box (mm), and parameter filters with pagination.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of element search in user's language for the Revit activity pane (e.g. 'Querying structural floors')."
                },
                category = new
                {
                    type = "string",
                    description = "Category name (e.g. 'Walls', 'OST_Walls')."
                },
                level = new
                {
                    type = "string",
                    description = "Level name or Level ID."
                },
                boundingBox = new
                {
                    type = "object",
                    description = "Bounding box in millimeters: { 'min': [x, y, z], 'max': [x, y, z] }."
                },
                parameters = new
                {
                    type = "array",
                    description = "Parameter filters: array of { 'name': '...', 'operator': 'Equals|Contains|StartsWith|GreaterThan|LessThan', 'value': '...' }."
                },
                limit = new
                {
                    type = "integer",
                    description = "Maximum number of elements to return (default 50, max 2000 when includeParameters=false, max 500 when includeParameters=true)."
                },
                offset = new
                {
                    type = "integer",
                    description = "Offset for pagination (default 0)."
                },
                fields = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "List of fields to return: 'id', 'category', 'parameters', 'boundingbox'."
                },
                mode = new
                {
                    type = "string",
                    description = "'elements' (default, return element list) or 'summary' (no element list, only aggregations/grouping/parameter audit).",
                    @enum = new[] { "elements", "summary" }
                },
                groupBy = new
                {
                    type = "array",
                    items = new
                    {
                        type = "string",
                        @enum = new[] { "category", "level", "workset", "family", "type" }
                    },
                    description = "Optional grouping dimensions for exact per-group counts (without 200-element trimming): 'category', 'level', 'workset', 'family', 'type'."
                },
                includeParameters = new
                {
                    type = "boolean",
                    description = "Whether to serialize the full parameter dictionary of each element (default true). Set false to keep responses small."
                },
                parameterNames = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "Optional list of parameter names to return (instead of the full dictionary)."
                },
                includeTypeParameters = new
                {
                    type = "boolean",
                    description = "Whether parameter filters and fill-rate checks also look at element type parameters (default false)."
                },
                parameterAudit = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "List of parameter names to compute fill rate for (e.g. ['ADSK_Marka', 'ADSK_Position'])."
                },
                resolveLevelFallback = new
                {
                    type = "boolean",
                    description = "Whether to resolve element level via fallback built-in parameters when LevelId is invalid (default true)."
                },
                checkLevelQuality = new
                {
                    type = "boolean",
                    description = "Whether to report elements missing a level and multi-level walls/columns (default false)."
                }
            }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<QueryElementsRequest>(json, context.JsonOptions) ?? new QueryElementsRequest();
            var result = await client.QueryElementsAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class GetBimMapTool : IMcpTool
    {
        public string Name => "get_bim_map";
        public string Description => "Retrieve hierarchical BIM-Map of the model (Levels, Grids, Links, Rooms, Category summary) for fast LLM spatial reasoning without scanning the whole database.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                scope = new
                {
                    type = "string",
                    description = "Detail scope: 'overview' (L0 topology: levels, grids, category summary), 'spatial' (L1 rooms and boundaries), or 'all'. Default 'overview'.",
                    @enum = new[] { "overview", "spatial", "all" }
                },
                levelId = new
                {
                    type = "integer",
                    description = "Optional level ID to filter spatial rooms by."
                },
                forceRefresh = new
                {
                    type = "boolean",
                    description = "Force re-extracting from active Revit document and updating SQLite cache (default false)."
                }
            }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<GetBimMapRequest>(json, context.JsonOptions) ?? new GetBimMapRequest();
            var result = await client.GetBimMapAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class SearchBimMapTool : IMcpTool
    {
        public string Name => "search_bim_map";
        public string Description => "Fast full-text search (SQLite FTS5) across all model elements, types, marks, comments, rooms, and categories in 1-2 ms without touching Revit API.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description = "Search query string (e.g. 'W-01', 'B20', '101'). Supports wildcard prefixes."
                },
                category = new
                {
                    type = "string",
                    description = "Optional category filter."
                },
                levelId = new
                {
                    type = "integer",
                    description = "Optional level ID filter."
                },
                limit = new
                {
                    type = "integer",
                    description = "Maximum results to return (default 50, max 200)."
                }
            },
            required = new[] { "query" }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<SearchBimMapRequest>(json, context.JsonOptions) ?? new SearchBimMapRequest();
            var result = await client.SearchBimMapAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class RebuildBimMapTool : IMcpTool
    {
        public string Name => "rebuild_bim_map";
        public string Description => "Force rebuild the complete BIM-Map SQLite database cache for the active Revit project.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                force = new
                {
                    type = "boolean",
                    description = "Force rebuild even if version GUID has not changed (default true)."
                }
            }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<RebuildBimMapRequest>(json, context.JsonOptions) ?? new RebuildBimMapRequest();
            var result = await client.RebuildBimMapAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class ManageCacheTool : IMcpTool
    {
        public string Name => "manage_cache";
        public string Description => "Inspect or clean RevitMCP SQLite BIM-Map cache files (%LocalAppData%/RevitMCP/models) to enforce disk quotas (2 GB LRU budget, 30 days retention).";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'status' to view cache usage, 'clean' to prune expired and oldest files.",
                    @enum = new[] { "status", "clean" }
                },
                maxSizeMb = new
                {
                    type = "integer",
                    description = "Disk budget cap in megabytes (default 2048 MB)."
                },
                retentionDays = new
                {
                    type = "integer",
                    description = "Max age in days before cache database is pruned (default 30 days)."
                }
            }
        };

        public Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<ManageCacheRequest>(json, context.JsonOptions) ?? new ManageCacheRequest();
            ManageCacheResult result;
            if (string.Equals(req.Action, "clean", StringComparison.OrdinalIgnoreCase))
            {
                result = CacheCleaner.Clean(req.MaxSizeMb, req.RetentionDays);
            }
            else
            {
                result = CacheCleaner.GetStatus();
            }
            return Task.FromResult(context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions)));
        }
    }
}
