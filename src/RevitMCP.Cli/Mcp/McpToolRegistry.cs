using System;
using System.Collections.Generic;
using System.Linq;
using RevitMCP.Cli.Mcp.Tools;

namespace RevitMCP.Cli.Mcp
{
    public class McpToolRegistry
    {
        private readonly Dictionary<string, IMcpTool> _tools = new(StringComparer.OrdinalIgnoreCase);

        public void Register(IMcpTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            _tools[tool.Name] = tool;
        }

        public bool TryGetTool(string name, out IMcpTool? tool)
        {
            return _tools.TryGetValue(name, out tool);
        }

        public IReadOnlyCollection<IMcpTool> GetAllTools() => _tools.Values;

        public object[] GetToolsListSchema()
        {
            return _tools.Values
                .Select(t => (object)new
                {
                    name = t.Name,
                    description = t.Description,
                    inputSchema = t.InputSchema
                })
                .ToArray();
        }

        public static McpToolRegistry CreateDefault()
        {
            var registry = new McpToolRegistry();

            // Core tools
            registry.Register(new PingTool());
            registry.Register(new ListRevitInstancesTool());
            registry.Register(new SelectRevitInstanceTool());
            registry.Register(new ExecuteCSharpTool());

            // Knowledge tools
            registry.Register(new RecordLessonTool());
            registry.Register(new QueryKnowledgeTool());

            // Document, Parameter & Schedule tools
            registry.Register(new ManageDocumentsTool());
            registry.Register(new InspectSchemaTool());
            registry.Register(new ManageParametersTool());
            registry.Register(new ExportScheduleTool());

            // Query & BIM-Map tools
            registry.Register(new QueryElementsTool());
            registry.Register(new GetBimMapTool());
            registry.Register(new SearchBimMapTool());
            registry.Register(new RebuildBimMapTool());
            registry.Register(new ManageCacheTool());

            // Modeling tools
            registry.Register(new TransformElementsTool());
            registry.Register(new CreateElementsTool());

            // Analysis, UI & Vision tools
            registry.Register(new ExportViewImageTool());
            registry.Register(new CheckIntersectionsTool());
            registry.Register(new ManageSelectionTool());
            registry.Register(new InspectElementGeometryTool());
            registry.Register(new VisualOverrideTool());

            return registry;
        }
    }
}
