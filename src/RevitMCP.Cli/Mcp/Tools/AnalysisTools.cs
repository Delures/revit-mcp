using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Cli.Mcp.Tools
{
    public class ExportViewImageTool : IMcpTool
    {
        public string Name => "export_view_image";
        public string Description => "Export an image (PNG or JPEG) of the active Revit view or a specified view. Returns Base64-encoded image data in standard MCP image format for multimodal LLM vision analysis.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary in the user's conversation language for the Revit dockable activity pane (e.g. 'Exporting 3D view snapshot')."
                },
                viewId = new
                {
                    type = "integer",
                    description = "Optional View ID. If omitted or null, exports the current active view."
                },
                format = new
                {
                    type = "string",
                    description = "Image format: 'png' or 'jpeg'. Default 'png'.",
                    @enum = new[] { "png", "jpeg" }
                },
                pixelSize = new
                {
                    type = "integer",
                    description = "Pixel size for the longest dimension (e.g. 1024, 2048). Default 1024."
                },
                dpi = new
                {
                    type = "integer",
                    description = "Resolution DPI (e.g. 72, 96, 150, 300). Default 96."
                },
                returnBase64 = new
                {
                    type = "boolean",
                    description = "Whether to return the Base64 image payload in MCP image content. Default true."
                },
                saveToDisk = new
                {
                    type = "boolean",
                    description = "Whether to save the image to disk. Default false."
                },
                outputPath = new
                {
                    type = "string",
                    description = "Optional custom destination file path if saveToDisk is true."
                }
            }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<ExportViewImageRequest>(json, context.JsonOptions) ?? new ExportViewImageRequest();
            var result = await client.ExportViewImageAsync(req, cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                return context.FormatMcpErrorResult(idValue, result.ErrorMessage ?? "Failed to export view image.");
            }

            if (!string.IsNullOrEmpty(result.Base64Data))
            {
                return JsonRpcResponse.Success(idValue, new
                {
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            data = result.Base64Data,
                            mimeType = result.MimeType
                        },
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(new
                            {
                                success = true,
                                viewId = result.ViewId,
                                viewName = result.ViewName,
                                format = result.Format,
                                width = result.Width,
                                height = result.Height,
                                fileSizeBytes = result.FileSizeBytes,
                                savedPath = result.SavedPath
                            }, context.JsonOptions)
                        }
                    },
                    isError = false
                });
            }

            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class CheckIntersectionsTool : IMcpTool
    {
        public string Name => "check_intersections";
        public string Description => "Check 3D geometric solid intersections and clashes between elements in Revit using native geometric engine (ElementIntersectsElementFilter, ElementIntersectsSolidFilter, BooleanOperationsUtils).";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of clash check in user's conversation language for the Revit activity pane (e.g. 'Checking slab clashes with columns')."
                },
                targetElementId = new
                {
                    type = "integer",
                    description = "Target element ID to check intersections against (e.g. floor/slab, wall, duct). Required for mode 'element'; ignored for 'matrix'/'duplicates'."
                },
                mode = new
                {
                    type = "string",
                    description = "'element' (default, single target vs candidates), 'matrix' (sourceCategories vs targetCategories), 'duplicates' (duplicate elements within sourceCategories).",
                    @enum = new[] { "element", "matrix", "duplicates" }
                },
                sourceCategories = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "For 'matrix'/'duplicates': source category names (BuiltInCategory e.g. 'OST_Walls' or Russian names)."
                },
                targetCategories = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "For 'matrix': target category names. Defaults to sourceCategories."
                },
                sourceElementIds = new
                {
                    type = "array",
                    items = new { type = "integer" },
                    description = "For 'matrix'/'duplicates': explicit source element IDs to test."
                },
                levelName = new
                {
                    type = "string",
                    description = "For 'matrix'/'duplicates': optional level name to restrict source elements to."
                },
                candidateCategories = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "Optional list of category names as displayed in the Revit UI in the current language (e.g. 'Columns', 'Walls', 'Ducts') or BuiltInCategory names (e.g. 'OST_StructuralColumns', 'OST_Walls', 'OST_DuctCurves')."
                },
                candidateElementIds = new
                {
                    type = "array",
                    items = new { type = "integer" },
                    description = "Optional explicit list of candidate element IDs to test against."
                },
                activeViewOnly = new
                {
                    type = "boolean",
                    description = "Whether to restrict search to elements on the active view only. Default false (entire model)."
                },
                calculateClashVolume = new
                {
                    type = "boolean",
                    description = "Whether to calculate exact 3D solid clash volume in cubic meters (m³) using BooleanOperationsUtils. Default false."
                },
                limit = new
                {
                    type = "integer",
                    description = "Maximum number of intersecting elements to return (default 100, max 1000)."
                },
                toleranceMm = new
                {
                    type = "number",
                    description = "For 'duplicates': AABB tolerance in mm for considering two elements duplicates (default 3.0 mm)."
                },
                minClashVolumeM3 = new
                {
                    type = "number",
                    description = "For 'matrix': minimum clash volume in m3 to report (default 0.0001)."
                },
                maxDurationMs = new
                {
                    type = "integer",
                    description = "For 'matrix': time budget in milliseconds before truncating (default 25000)."
                }
            }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<CheckIntersectionsRequest>(json, context.JsonOptions) ?? new CheckIntersectionsRequest();
            var result = await client.CheckIntersectionsAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class ManageSelectionTool : IMcpTool
    {
        public string Name => "manage_selection";
        public string Description => "Get or set the active element selection in the Revit UI. Allows the agent to see what the user currently has selected, or highlight elements for the user.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of selection in user's language for the Revit activity pane (e.g. 'Selecting slab in active view')."
                },
                action = new
                {
                    type = "string",
                    description = "'get' to read currently selected elements in Revit UI, 'set' to highlight a list of element IDs (default 'get').",
                    @enum = new[] { "get", "set" }
                },
                elementIds = new
                {
                    type = "array",
                    items = new { type = "integer" },
                    description = "Array of element IDs to select (for 'set')."
                }
            }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<ManageSelectionRequest>(json, context.JsonOptions) ?? new ManageSelectionRequest();
            var result = await client.ManageSelectionAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class InspectElementGeometryTool : IMcpTool
    {
        public string Name => "inspect_element_geometry";
        public string Description => "Extract precise analytical geometry of an element in millimeters (mm). For slabs: boundary polygon points, internal holes, thickness, Z elevations; for walls/beams: location curve start/end, length, orientation; for columns: insertion point; for solids: volume m3, centroid, planar face loops.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of geometry inspection in user's language for the Revit activity pane (e.g. 'Inspecting slab geometry ID 2969454')."
                },
                elementId = new
                {
                    type = "integer",
                    description = "Revit element ID to inspect."
                },
                includeFaces = new
                {
                    type = "boolean",
                    description = "Whether to extract detailed planar face loops and normals. Default false."
                }
            },
            required = new[] { "elementId" }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<InspectElementGeometryRequest>(json, context.JsonOptions) ?? new InspectElementGeometryRequest();
            var result = await client.InspectElementGeometryAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class VisualOverrideTool : IMcpTool
    {
        public string Name => "visual_override";
        public string Description => "Control view graphics, element visibility, colors, transparency, and rebar 3D solid mode on the active view. Actions: 'override_graphics' (color, transparency 0..100, halftone), 'reset_graphics', 'isolate' (temporary isolate), 'hide', 'unhide', 'reset_isolate', 'set_rebar_presentation' (solidIn3D, unobscuredInView).";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of this graphics operation in the user's conversation language for the Revit dockable activity pane (e.g. 'Highlighting rebar in red and making slab transparent')."
                },
                transactionName = new
                {
                    type = "string",
                    description = "Short human-readable transaction name in the user's conversation language for Revit Undo (e.g. 'Highlight Reinforcement', 'Bewehrung hervorheben')."
                },
                action = new
                {
                    type = "string",
                    description = "Graphic action to execute.",
                    @enum = new[] { "override_graphics", "reset_graphics", "isolate", "hide", "unhide", "reset_isolate", "set_rebar_presentation" }
                },
                elementIds = new
                {
                    type = "array",
                    items = new { type = "integer" },
                    description = "Array of element IDs to affect."
                },
                viewId = new
                {
                    type = "integer",
                    description = "Optional View ID. Defaults to active view."
                },
                transparency = new
                {
                    type = "integer",
                    description = "Surface transparency percentage (0 = opaque, 100 = completely transparent)."
                },
                color = new
                {
                    type = "object",
                    description = "RGB projection/surface color: { r: 0..255, g: 0..255, b: 0..255 }.",
                    properties = new
                    {
                        r = new { type = "integer" },
                        g = new { type = "integer" },
                        b = new { type = "integer" }
                    },
                    required = new[] { "r", "g", "b" }
                },
                halftone = new
                {
                    type = "boolean",
                    description = "Whether to display element as halftone."
                },
                solidIn3D = new
                {
                    type = "boolean",
                    description = "For Rebar: display as solid in 3D view."
                },
                unobscuredInView = new
                {
                    type = "boolean",
                    description = "For Rebar: display unobscured (visible through host concrete)."
                }
            },
            required = new[] { "action", "elementIds" }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<VisualOverrideRequest>(json, context.JsonOptions) ?? new VisualOverrideRequest();
            var result = await client.VisualOverrideAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }
}
