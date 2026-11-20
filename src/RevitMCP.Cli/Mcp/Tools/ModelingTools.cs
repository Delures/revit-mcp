using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Cli.Mcp.Tools
{
    public class TransformElementsTool : IMcpTool
    {
        public string Name => "transform_elements";
        public string Description => "Transform existing elements in Revit: move by vector (in mm), rotate around axis by angle (degrees), copy by vector (in mm), or delete elements.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of this operation in the user's conversation language for the Revit dockable activity pane (e.g. 'Deleting test rebars (37 items)', 'Löschen von Bewehrungselementen')."
                },
                transactionName = new
                {
                    type = "string",
                    description = "Short human-readable transaction name in the user's conversation language for the Revit Undo history stack (e.g. 'Delete Rebars', 'Bewehrung löschen')."
                },
                action = new
                {
                    type = "string",
                    description = "Transformation type: 'move', 'rotate', 'copy', 'delete'.",
                    @enum = new[] { "move", "rotate", "copy", "delete" }
                },
                elementIds = new
                {
                    type = "array",
                    items = new { type = "integer" },
                    description = "List of Revit element IDs to transform."
                },
                translation = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new { type = "number", description = "Offset X in millimeters." },
                        y = new { type = "number", description = "Offset Y in millimeters." },
                        z = new { type = "number", description = "Offset Z in millimeters." }
                    },
                    description = "Displacement vector in millimeters (for 'move' and 'copy')."
                },
                angleDegrees = new
                {
                    type = "number",
                    description = "Rotation angle in degrees (for 'rotate'). Positive is counter-clockwise."
                },
                rotationAxisOrigin = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new { type = "number" },
                        y = new { type = "number" },
                        z = new { type = "number" }
                    },
                    description = "Origin point of rotation axis in mm (default center 0, 0, 0)."
                },
                rotationAxisDirection = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new { type = "number" },
                        y = new { type = "number" },
                        z = new { type = "number" }
                    },
                    description = "Rotation axis vector (default vertical Z: 0, 0, 1)."
                }
            },
            required = new[] { "action", "elementIds" }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<TransformElementsRequest>(json, context.JsonOptions) ?? new TransformElementsRequest();
            var result = await client.TransformElementsAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }

    public class CreateElementsTool : IMcpTool
    {
        public string Name => "create_elements";
        public string Description => "Create BIM and Structural elements in Revit: walls, slabs/floors (polygonal with holes in mm), beams (linear structural framing in mm), columns (vertical structural columns with offsets and rotation), area reinforcement (multi-layer with spacings), rebars (shape-driven/curves), levels, grids, family instances, rooms, or model curves.";
        public object InputSchema => new
        {
            type = "object",
            properties = new
            {
                actionSummary = new
                {
                    type = "string",
                    description = "Human-readable summary of what you are creating, written in the user's conversation language for the Revit dockable activity pane (e.g. 'Creating slab reinforcement mesh', 'Erstellen der Deckenbewehrung')."
                },
                transactionName = new
                {
                    type = "string",
                    description = "Short human-readable transaction name written in the user's conversation language for Revit Undo (e.g. 'Slab Reinforcement', 'Bewehrung Decke')."
                },
                action = new
                {
                    type = "string",
                    description = "Element creation type: 'wall', 'level', 'grid', 'family_instance', 'room', 'model_curve', 'slab', 'beam', 'column', 'area_reinforcement', 'rebar'.",
                    @enum = new[] { "wall", "level", "grid", "family_instance", "room", "model_curve", "slab", "beam", "column", "area_reinforcement", "rebar" }
                },
                wall = new
                {
                    type = "object",
                    properties = new
                    {
                        startPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, required = new[] { "x", "y" } },
                        endPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, required = new[] { "x", "y" } },
                        levelId = new { type = "integer" },
                        wallTypeId = new { type = "integer" },
                        height = new { type = "number", description = "Wall height in millimeters (default 3000)." },
                        offset = new { type = "number", description = "Base offset in millimeters (default 0)." },
                        isStructural = new { type = "boolean" }
                    },
                    required = new[] { "startPoint", "endPoint" }
                },
                slab = new
                {
                    type = "object",
                    properties = new
                    {
                        boundaryPoints = new { type = "array", items = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, required = new[] { "x", "y" } }, description = "List of boundary polygon vertices in mm." },
                        holes = new { type = "array", items = new { type = "array", items = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, required = new[] { "x", "y" } } }, description = "Optional inner opening loops in mm." },
                        levelId = new { type = "integer", description = "Level ID." },
                        floorTypeId = new { type = "integer", description = "Optional FloorType ID." },
                        heightOffset = new { type = "number", description = "Height offset from level in mm (default 0)." },
                        isStructural = new { type = "boolean", description = "Whether floor is structural (default true)." }
                    },
                    required = new[] { "boundaryPoints" }
                },
                beam = new
                {
                    type = "object",
                    properties = new
                    {
                        startPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, required = new[] { "x", "y", "z" } },
                        endPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, required = new[] { "x", "y", "z" } },
                        levelId = new { type = "integer", description = "Reference Level ID." },
                        familySymbolId = new { type = "integer", description = "Optional framing family symbol ID." },
                        familyName = new { type = "string", description = "Framing family name (e.g. 'Concrete beam')." },
                        typeName = new { type = "string", description = "Framing type name (e.g. '400x600')." }
                    },
                    required = new[] { "startPoint", "endPoint" }
                },
                column = new
                {
                    type = "object",
                    properties = new
                    {
                        location = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, required = new[] { "x", "y" } },
                        baseLevelId = new { type = "integer", description = "Base Level ID." },
                        topLevelId = new { type = "integer", description = "Top Level ID." },
                        baseOffset = new { type = "number", description = "Base offset in mm." },
                        topOffset = new { type = "number", description = "Top offset in mm." },
                        rotationDegrees = new { type = "number", description = "Rotation angle in degrees." },
                        familySymbolId = new { type = "integer", description = "Optional column family symbol ID." },
                        familyName = new { type = "string", description = "Column family name (e.g. 'Concrete column')." },
                        typeName = new { type = "string", description = "Column type name (e.g. '400x400')." }
                    },
                    required = new[] { "location", "baseLevelId" }
                },
                areaReinforcement = new
                {
                    type = "object",
                    properties = new
                    {
                        hostElementId = new { type = "integer", description = "Host slab or wall element ID." },
                        majorDirection = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, description = "Major rebar direction vector." },
                        boundaryCurves = new { type = "array", items = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } } }, description = "Optional boundary polygon vertices in mm." },
                        areaReinforcementTypeId = new { type = "integer" },
                        topMajorBarTypeId = new { type = "integer" },
                        topMinorBarTypeId = new { type = "integer" },
                        bottomMajorBarTypeId = new { type = "integer" },
                        bottomMinorBarTypeId = new { type = "integer" },
                        topMajorSpacing = new { type = "number", description = "Top major rebar spacing in mm (default 200)." },
                        topMinorSpacing = new { type = "number", description = "Top minor rebar spacing in mm (default 200)." },
                        bottomMajorSpacing = new { type = "number", description = "Bottom major rebar spacing in mm (default 200)." },
                        bottomMinorSpacing = new { type = "number", description = "Bottom minor rebar spacing in mm (default 200)." }
                    },
                    required = new[] { "hostElementId" }
                },
                rebar = new
                {
                    type = "object",
                    properties = new
                    {
                        hostElementId = new { type = "integer", description = "Host element ID." },
                        barTypeId = new { type = "integer", description = "RebarBarType element ID." },
                        curveSegments = new { type = "array", items = new { type = "array", items = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } } } }, description = "List of curve segments defining rebar shape in mm." },
                        normal = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, description = "Normal vector to rebar plane (default Z: 0, 0, 1)." },
                        layoutRule = new { type = "string", description = "Layout rule: 'Single', 'MaximumSpacing', 'FixedNumber', 'NumberWithSpacing'.", @enum = new[] { "Single", "MaximumSpacing", "FixedNumber", "NumberWithSpacing" } },
                        spacing = new { type = "number", description = "Spacing between bars in mm." },
                        quantity = new { type = "integer", description = "Number of bars in array." },
                        arrayLength = new { type = "number", description = "Array distribution length in mm." }
                    },
                    required = new[] { "hostElementId", "curveSegments" }
                },
                level = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string" },
                        elevation = new { type = "number", description = "Level elevation in millimeters." }
                    },
                    required = new[] { "elevation" }
                },
                grid = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string" },
                        startPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" } }, required = new[] { "x", "y" } },
                        endPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" } }, required = new[] { "x", "y" } }
                    },
                    required = new[] { "startPoint", "endPoint" }
                },
                familyInstance = new
                {
                    type = "object",
                    properties = new
                    {
                        familySymbolId = new { type = "integer" },
                        familyName = new { type = "string" },
                        typeName = new { type = "string" },
                        location = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, description = "Insertion location in mm." },
                        startPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, description = "Start point for linear/curve-based families in mm." },
                        endPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, description = "End point for linear/curve-based families in mm." },
                        referenceDirection = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, description = "Orientation direction vector (e.g. { x: 1, y: 0, z: 0 })." },
                        levelId = new { type = "integer" },
                        hostElementId = new { type = "integer" },
                        viewId = new { type = "integer", description = "Optional View ID for view-specific family instances." },
                        structuralType = new { type = "string", description = "Structural type: 'NonStructural', 'Beam', 'Column', 'Bracing', 'Footing'.", @enum = new[] { "NonStructural", "Beam", "Column", "Bracing", "Footing", "Unknown" } },
                        rotationDegrees = new { type = "number", description = "Rotation around vertical Z-axis in degrees." }
                    }
                },
                room = new
                {
                    type = "object",
                    properties = new
                    {
                        levelId = new { type = "integer" },
                        point = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" } }, required = new[] { "x", "y" } },
                        name = new { type = "string" },
                        number = new { type = "string" }
                    },
                    required = new[] { "levelId", "point" }
                },
                modelCurve = new
                {
                    type = "object",
                    properties = new
                    {
                        startPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, required = new[] { "x", "y" } },
                        endPoint = new { type = "object", properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } }, required = new[] { "x", "y" } }
                    },
                    required = new[] { "startPoint", "endPoint" }
                }
            },
            required = new[] { "action" }
        };

        public async Task<JsonRpcResponse> ExecuteAsync(McpToolContext context, object? idValue, JsonNode? arguments, CancellationToken cancellationToken)
        {
            var client = await context.Router.GetClientAsync(cancellationToken).ConfigureAwait(false);
            string json = arguments?.ToJsonString() ?? "{}";
            var req = JsonSerializer.Deserialize<CreateElementsRequest>(json, context.JsonOptions) ?? new CreateElementsRequest();
            var result = await client.CreateElementsAsync(req, cancellationToken).ConfigureAwait(false);
            return context.FormatMcpTextResult(idValue, JsonSerializer.Serialize(result, context.JsonOptions));
        }
    }
}
