using System.Collections.Generic;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Shared.Commands
{
    public class ParameterSpec
    {
        public string? Name { get; set; }
        public string? BuiltInName { get; set; }
        public string? Guid { get; set; }
        public object? Value { get; set; }
        public string? Unit { get; set; }
    }

    public class ManageParametersRequest : IAgentTransactionAction
    {
        public string Action { get; set; } = "get"; // "get" or "set"
        public string? ActionSummary { get; set; }
        public string? AgentName { get; set; }
        public string? TransactionName { get; set; }
        public List<long> ElementIds { get; set; } = new List<long>();
        public List<ParameterSpec> Parameters { get; set; } = new List<ParameterSpec>();
    }

    public class ParameterValueResult
    {
        public string Name { get; set; } = string.Empty;
        public object? Value { get; set; }
        public string? DisplayValue { get; set; }
        public string StorageType { get; set; } = string.Empty;
        public bool IsReadOnly { get; set; }
        public string? Unit { get; set; }
        public bool? Updated { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ElementParametersResult
    {
        public long ElementId { get; set; }
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public Dictionary<string, ParameterValueResult> Parameters { get; set; } = new Dictionary<string, ParameterValueResult>();
    }

    public class ManageParametersResult
    {
        public bool Success { get; set; } = true;
        public string Action { get; set; } = string.Empty;
        public List<ElementParametersResult> Elements { get; set; } = new List<ElementParametersResult>();
        public int TotalUpdated { get; set; }
        public List<WarningRecord>? Warnings { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
