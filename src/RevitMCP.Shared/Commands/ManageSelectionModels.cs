using System.Collections.Generic;

namespace RevitMCP.Shared.Commands
{
    public class ManageSelectionRequest : IAgentAction
    {
        public string Action { get; set; } = "get"; // "get" or "set"
        public List<int>? ElementIds { get; set; }
        public string? ActionSummary { get; set; }
        public string? AgentName { get; set; }
    }

    public class SelectedElementInfo
    {
        public int Id { get; set; }
        public string? Category { get; set; }
        public string? FamilyName { get; set; }
        public string? TypeName { get; set; }
        public string? Name { get; set; }
    }

    public class ManageSelectionResult
    {
        public bool Success { get; set; }
        public string Action { get; set; } = "get";
        public List<int> SelectedElementIds { get; set; } = new List<int>();
        public List<SelectedElementInfo>? Elements { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
